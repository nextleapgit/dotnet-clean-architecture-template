using System.Text;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.Email;
using CleanArchitecture.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.IntegrationTests.Email;

/// <summary>
/// Storage and dispatch against a real database. Each test owns the rows it creates and deletes only
/// those. Owned rows are made the oldest due rows, so claims pick them before anything else.
/// </summary>
public sealed class EmailOutboxTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory), IAsyncLifetime
{
    private const string Table = "public.email_outbox_messages";

    private readonly List<Guid> _ownedIds = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (Guid id in _ownedIds)
        {
            await WithDbContextAsync(db => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM " + Table + " WHERE id = {0}",
                [id],
                CancellationToken.None));
        }
    }

    private static EmailMessage NewMessage(string subject = "Welcome — مرحباً") =>
        EmailMessage.Create(Guid.CreateVersion7(), "recipient@example.com", subject, "Text body", "<p>Html body</p>").Value;

    private async Task<EmailMessage> EnqueueOwnedAsync(EmailMessage? message = null, DateTime? expiresAtUtc = null)
    {
        message ??= NewMessage();
        _ownedIds.Add(message.Id);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(CancellationToken))
        {
            await scope.ServiceProvider.GetRequiredService<IEmailOutbox>()
                .EnqueueAsync(message, expiresAtUtc ?? DateTime.UtcNow.AddDays(1), CancellationToken);
            await transaction.CommitAsync(CancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE " + Table + " SET next_attempt_at_utc = '1970-01-01T00:00:00Z' WHERE id = {0}",
            [message.Id],
            CancellationToken);

        return message;
    }

    private Task<EmailOutboxMessage> RowAsync(Guid id) =>
        WithDbContextAsync(db => db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, CancellationToken));

    private Task<int> ExecuteAsync(string sql, params object[] parameters) =>
        WithDbContextAsync(db => db.Database.ExecuteSqlRawAsync(sql, parameters, CancellationToken));

    private async Task<bool> DispatchOneAsync(IEmailSender sender)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        var dispatcher = new EmailOutboxDispatcher(
            services.GetRequiredService<EmailOutboxStore>(),
            services.GetRequiredService<EmailPayloadProtector>(),
            sender,
            Options.Create(new EmailOutboxOptions { Enabled = true }),
            Options.Create(new SmtpOptions()),
            NullLogger<EmailOutboxDispatcher>.Instance);

        return await dispatcher.DispatchOneAsync(CancellationToken);
    }

    // ---------------------------------------------------------------- enqueue

    [Fact]
    public async Task Enqueue_Should_Throw_WhenNoTransactionIsActive()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IEmailOutbox outbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();

        await Should.ThrowAsync<InvalidOperationException>(
            () => outbox.EnqueueAsync(NewMessage(), DateTime.UtcNow.AddDays(1), CancellationToken));
    }

    [Fact]
    public async Task Enqueue_Should_LeaveNoRow_WhenTransactionRollsBack()
    {
        EmailMessage message = NewMessage();

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IEmailOutbox>()
                .EnqueueAsync(message, DateTime.UtcNow.AddDays(1), CancellationToken);
            // Disposed without commit: rolled back.
        }

        (await WithDbContextAsync(db => db.EmailOutboxMessages.AnyAsync(m => m.Id == message.Id, CancellationToken)))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Enqueue_Should_StoreOnlyAProtectedPayloadThatRoundTripsNonAsciiContent()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        EmailOutboxMessage row = await RowAsync(message.Id);
        row.Status.ShouldBe(EmailOutboxStatus.Pending);
        row.AttemptCount.ShouldBe(0);
        Encoding.UTF8.GetString(row.Payload!).ShouldNotContain("recipient@example.com");

        EmailPayloadProtector protector = Factory.Services.GetRequiredService<EmailPayloadProtector>();
        EmailMessage roundTripped = protector.Unprotect(row.Id, row.ExpiresAtUtc, row.Payload!).Value;
        roundTripped.ShouldBe(message);
    }

    [Fact]
    public async Task Payload_Should_BeUnreadable_WhenExpiryIsExtended()
    {
        EmailMessage message = await EnqueueOwnedAsync();
        EmailOutboxMessage row = await RowAsync(message.Id);
        EmailPayloadProtector protector = Factory.Services.GetRequiredService<EmailPayloadProtector>();

        Result<EmailMessage> result = protector.Unprotect(row.Id, row.ExpiresAtUtc.AddDays(1), row.Payload!);

        result.Error.Code.ShouldBe(EmailErrorCodes.PayloadProtectionFailure);
    }

    [Fact]
    public async Task Enqueue_Should_RejectADuplicateLogicalId()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await Should.ThrowAsync<DbUpdateException>(() => EnqueueOwnedAsync(message));
    }

    // ---------------------------------------------------------------- claims and outcomes

    [Fact]
    public async Task ClaimedRow_Should_BeLockedFromASecondConnection()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await using AsyncServiceScope holder = Factory.Services.CreateAsyncScope();
        ApplicationDbContext holderDb = holder.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDbContextTransaction transaction = await holderDb.Database.BeginTransactionAsync(CancellationToken);

        ClaimedEmail? claimed = await holder.ServiceProvider.GetRequiredService<EmailOutboxStore>()
            .ClaimNextAsync(Guid.NewGuid(), 60, 8, CancellationToken);

        string lockProbe = "SELECT id AS \"Value\" FROM " + Table + " WHERE id = {0} FOR UPDATE SKIP LOCKED";
        List<Guid> visibleWhileLocked = await WithDbContextAsync(db =>
            db.Database.SqlQueryRaw<Guid>(lockProbe, message.Id).ToListAsync(CancellationToken));

        await transaction.CommitAsync(CancellationToken);
        await transaction.DisposeAsync();

        List<Guid> visibleAfterCommit = await WithDbContextAsync(db =>
            db.Database.SqlQueryRaw<Guid>(lockProbe, message.Id).ToListAsync(CancellationToken));

        claimed!.Id.ShouldBe(message.Id);
        visibleWhileLocked.ShouldBeEmpty();
        visibleAfterCommit.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Completion_Should_OnlySucceedOnceAndOnlyForTheLeaseOwner()
    {
        EmailMessage message = await EnqueueOwnedAsync();
        var leaseId = Guid.NewGuid();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        EmailOutboxStore store = scope.ServiceProvider.GetRequiredService<EmailOutboxStore>();

        ClaimedEmail? claimed = await store.ClaimNextAsync(leaseId, 60, 8, CancellationToken);
        bool strangerWins = await store.MarkSentAsync(message.Id, Guid.NewGuid(), CancellationToken);
        bool ownerWins = await store.MarkSentAsync(message.Id, leaseId, CancellationToken);
        bool secondCompletionWins = await store.MarkSentAsync(message.Id, leaseId, CancellationToken);

        claimed!.Id.ShouldBe(message.Id);
        claimed.AttemptCount.ShouldBe(1);
        strangerWins.ShouldBeFalse();
        ownerWins.ShouldBeTrue();
        secondCompletionWins.ShouldBeFalse();

        EmailOutboxMessage row = await RowAsync(message.Id);
        row.Status.ShouldBe(EmailOutboxStatus.Sent);
        row.Payload.ShouldBeNull();
        row.LeaseId.ShouldBeNull();
        row.ProcessedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExpiredLease_Should_MakeTheRowClaimableAgain()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        EmailOutboxStore store = scope.ServiceProvider.GetRequiredService<EmailOutboxStore>();

        await store.ClaimNextAsync(Guid.NewGuid(), 60, 8, CancellationToken);
        await ExecuteAsync("UPDATE " + Table + " SET lease_expires_at_utc = now() - interval '1 second' WHERE id = {0}", message.Id);

        ClaimedEmail? reclaimed = await store.ClaimNextAsync(Guid.NewGuid(), 60, 8, CancellationToken);

        reclaimed!.Id.ShouldBe(message.Id);
        reclaimed.AttemptCount.ShouldBe(2);
    }

    // ---------------------------------------------------------------- dispatch

    [Fact]
    public async Task Dispatch_Should_SendDecryptedMessageAndMarkItSent()
    {
        EmailMessage message = await EnqueueOwnedAsync();
        var sender = new FakeEmailSender(EmailSendResult.Accepted);

        bool dispatched = await DispatchOneAsync(sender);

        dispatched.ShouldBeTrue();
        sender.Sent.ShouldHaveSingleItem().ShouldBe(message);
        (await RowAsync(message.Id)).Status.ShouldBe(EmailOutboxStatus.Sent);
    }

    [Fact]
    public async Task Dispatch_Should_ScheduleARetry_OnTemporaryFailure()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await DispatchOneAsync(new FakeEmailSender(EmailSendResult.TemporaryFailure(EmailErrorCodes.SmtpConnectionFailure)));

        EmailOutboxMessage row = await RowAsync(message.Id);
        row.Status.ShouldBe(EmailOutboxStatus.Pending);
        row.AttemptCount.ShouldBe(1);
        row.ErrorCode.ShouldBe(EmailErrorCodes.SmtpConnectionFailure);
        row.Payload.ShouldNotBeNull();
        row.LeaseId.ShouldBeNull();
        row.NextAttemptAtUtc.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task Dispatch_Should_FailPermanently_OnPermanentFailure()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await DispatchOneAsync(new FakeEmailSender(EmailSendResult.PermanentFailure(EmailErrorCodes.SmtpPermanentFailure)));

        EmailOutboxMessage row = await RowAsync(message.Id);
        row.Status.ShouldBe(EmailOutboxStatus.Failed);
        row.ErrorCode.ShouldBe(EmailErrorCodes.SmtpPermanentFailure);
        row.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task Dispatch_Should_TreatASenderTimeoutAsTemporary()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await DispatchOneAsync(new FakeEmailSender(new OperationCanceledException()));

        (await RowAsync(message.Id)).ErrorCode.ShouldBe(EmailErrorCodes.SmtpTimeout);
    }

    // ---------------------------------------------------------------- settlement, cleanup, health

    [Fact]
    public async Task Settle_Should_ExpireOverdueAndFailExhaustedMessages()
    {
        EmailMessage overdue = await EnqueueOwnedAsync();
        EmailMessage exhausted = await EnqueueOwnedAsync();
        await ExecuteAsync("UPDATE " + Table + " SET expires_at_utc = now() - interval '1 minute' WHERE id = {0}", overdue.Id);
        await ExecuteAsync("UPDATE " + Table + " SET attempt_count = 8 WHERE id = {0}", exhausted.Id);

        await WithDbContextAsync(async db =>
            await new EmailOutboxStore(db).SettleAsync(100, 8, CancellationToken));

        EmailOutboxMessage expiredRow = await RowAsync(overdue.Id);
        expiredRow.Status.ShouldBe(EmailOutboxStatus.Expired);
        expiredRow.ErrorCode.ShouldBe(EmailErrorCodes.DeliveryWindowElapsed);
        expiredRow.Payload.ShouldBeNull();

        EmailOutboxMessage failedRow = await RowAsync(exhausted.Id);
        failedRow.Status.ShouldBe(EmailOutboxStatus.Failed);
        failedRow.ErrorCode.ShouldBe(EmailErrorCodes.AttemptsExhausted);
    }

    [Fact]
    public async Task Cleanup_Should_DeleteOnlyTerminalRowsPastRetention()
    {
        EmailMessage old = await EnqueueOwnedAsync();
        EmailMessage recent = await EnqueueOwnedAsync();
        const string markSent = "UPDATE " + Table + " SET status = 2, payload = NULL, processed_at_utc = {1} WHERE id = {0}";
        await ExecuteAsync(markSent, old.Id, DateTime.UtcNow.AddDays(-30));
        await ExecuteAsync(markSent, recent.Id, DateTime.UtcNow);

        await WithDbContextAsync(db => new EmailOutboxStore(db).DeleteTerminalAsync(500, 14, CancellationToken));

        (await WithDbContextAsync(db => db.EmailOutboxMessages.AnyAsync(m => m.Id == old.Id, CancellationToken))).ShouldBeFalse();
        (await WithDbContextAsync(db => db.EmailOutboxMessages.AnyAsync(m => m.Id == recent.Id, CancellationToken))).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckConstraints_Should_RejectAPayloadOnATerminalRow()
    {
        EmailMessage message = await EnqueueOwnedAsync();

        await Should.ThrowAsync<Npgsql.PostgresException>(() => ExecuteAsync(
            "UPDATE " + Table + " SET status = 2, processed_at_utc = now() WHERE id = {0}",
            message.Id));
    }

    [Fact]
    public async Task HealthCheck_Should_ReportDegraded_WhenASendableEmailWaitsTooLong()
    {
        await EnqueueOwnedAsync();

        HealthCheckResult enabled = await WithDbContextAsync(db =>
            new EmailOutboxHealthCheck(
                    new EmailOutboxStore(db),
                    Options.Create(new EmailOutboxOptions { Enabled = true, HealthBacklogThresholdSeconds = 60 }))
                .CheckHealthAsync(new HealthCheckContext(), CancellationToken));

        HealthCheckResult disabled = await WithDbContextAsync(db =>
            new EmailOutboxHealthCheck(new EmailOutboxStore(db), Options.Create(new EmailOutboxOptions()))
                .CheckHealthAsync(new HealthCheckContext(), CancellationToken));

        enabled.Status.ShouldBe(HealthStatus.Degraded);
        disabled.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Registration_Should_EnqueueTheWelcomeEmail()
    {
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        EmailPayloadProtector protector = Factory.Services.GetRequiredService<EmailPayloadProtector>();
        List<EmailOutboxMessage> pending = await WithDbContextAsync(db => db.EmailOutboxMessages.AsNoTracking()
            .Where(m => m.Status == EmailOutboxStatus.Pending)
            .ToListAsync(CancellationToken));

        EmailMessage welcome = pending
            .Select(row => protector.Unprotect(row.Id, row.ExpiresAtUtc, row.Payload!))
            .Where(result => result.IsSuccess)
            .Select(result => result.Value)
            .Single(message => message.Recipient == email);

        welcome.Subject.ShouldBe("Welcome");
        _ownedIds.Add(welcome.Id);
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    private readonly EmailSendResult? _result;
    private readonly Exception? _exception;

    public FakeEmailSender(EmailSendResult result) => _result = result;

    public FakeEmailSender(Exception exception) => _exception = exception;

    public List<EmailMessage> Sent { get; } = [];

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (_exception is not null)
        {
            throw _exception;
        }

        lock (Sent)
        {
            Sent.Add(message);
        }

        return Task.FromResult(_result!);
    }
}
