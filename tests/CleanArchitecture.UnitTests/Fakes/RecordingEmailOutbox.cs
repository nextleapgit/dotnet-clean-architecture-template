using CleanArchitecture.BuildingBlocks.Email;

namespace CleanArchitecture.UnitTests.Fakes;

/// <summary>Enforces the real outbox contract: enqueueing requires an active transaction.</summary>
public sealed class RecordingEmailOutbox(FakeUnitOfWork unitOfWork) : IEmailOutbox
{
    public List<(EmailMessage Message, DateTime ExpiresAtUtc)> Enqueued { get; } = [];

    public Task EnqueueAsync(EmailMessage message, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        if (!unitOfWork.HasActiveTransaction)
        {
            throw new InvalidOperationException("Emails must be enqueued inside a transaction.");
        }

        Enqueued.Add((message, expiresAtUtc));

        return Task.CompletedTask;
    }
}
