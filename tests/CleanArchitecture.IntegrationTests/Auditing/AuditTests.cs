using System.Net.Http.Json;
using CleanArchitecture.Application.Users;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CleanArchitecture.IntegrationTests.Auditing;

public sealed class AuditTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private Task<List<AuditEntry>> EntriesForUserAsync(Guid userId) =>
        WithDbContextAsync(db => db.AuditEntries.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(CancellationToken));

    [Fact]
    public async Task AuthenticationEvents_Should_BeAuditedWithRequestContext()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await CreateUserAsync(email);

        using var request = new HttpRequestMessage(HttpMethod.Post, "users/login")
        {
            Content = JsonContent.Create(new { email, password = "WrongPassword1" })
        };
        request.Headers.Add("Correlation-Id", "audit-test-correlation");
        request.Headers.UserAgent.ParseAdd("IntegrationTests/1.0");

        // Act
        await HttpClient.SendAsync(request, CancellationToken);
        await LoginAsync(email);

        // Assert
        List<AuditEntry> entries = await EntriesForUserAsync(userId);
        entries.Select(e => e.Action).ShouldBe(
        [
            UserAuditActions.InvitationAccepted,
            UserAuditActions.LoginFailed,
            UserAuditActions.LoginSucceeded
        ]);
        entries.ShouldAllBe(e => e.TenantId != null && e.EntityName == "User");

        // The creation is attributed to the admin who created the user, in the user's tenant.
        AuditEntry created = await WithDbContextAsync(db => db.AuditEntries.AsNoTracking()
            .SingleAsync(a => a.Action == UserAuditActions.Created && a.EntityId == userId.ToString(), CancellationToken));
        created.UserId.ShouldNotBe(userId);
        created.TenantId.ShouldBe(entries[0].TenantId);

        AuditEntry failed = entries[1];
        failed.Severity.ShouldBe(AuditSeverity.Warning);
        failed.CorrelationId.ShouldBe("audit-test-correlation");
        failed.UserAgent.ShouldBe("IntegrationTests/1.0");
        failed.Metadata!.ShouldContain("invalid_password");
        failed.CreatedAtUtc.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task RefreshTokenReuse_Should_BeAuditedAsCritical()
    {
        // Arrange
        Account account = await CreateAccountAsync();
        await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);

        // Act
        await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);

        // Assert
        AuditEntry reuse = (await EntriesForUserAsync(account.UserId))
            .Single(e => e.Action == UserAuditActions.RefreshTokenReuseDetected);
        reuse.Severity.ShouldBe(AuditSeverity.Critical);
    }

    [Fact]
    public async Task RefreshAfterLogout_Should_NotRaiseAReuseAlarm()
    {
        // Arrange
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);
        await HttpClient.PostAsJsonAsync("users/logout", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);

        // Act
        await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);

        // Assert
        (await EntriesForUserAsync(account.UserId))
            .ShouldNotContain(e => e.Action == UserAuditActions.RefreshTokenReuseDetected);
    }

    [Theory]
    [InlineData("UPDATE public.audit_entries SET action = 'tampered'")]
    [InlineData("DELETE FROM public.audit_entries")]
    [InlineData("TRUNCATE public.audit_entries")]
    public async Task AuditEntries_Should_BeAppendOnlyInTheDatabase(string sql)
    {
        // Arrange
        await CreateUserAsync(UniqueEmail());

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(
            () => WithDbContextAsync(db => db.Database.ExecuteSqlRawAsync(sql, CancellationToken)));

        // Assert
        exception.MessageText.ShouldContain("append-only");
    }
}
