using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Infrastructure.Database;

namespace CleanArchitecture.Infrastructure.Email;

internal sealed class EmailOutbox(ApplicationDbContext dbContext, EmailPayloadProtector payloadProtector)
    : IEmailOutbox
{
    public async Task EnqueueAsync(EmailMessage message, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        // Only a transaction on this very context makes the enqueue atomic with the state change.
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Emails must be enqueued inside an explicit transaction on the current unit of work.");
        }

        DateTime expiry = UtcTimestamps.ToWholeMilliseconds(expiresAtUtc);

        dbContext.EmailOutboxMessages.Add(
            EmailOutboxMessage.Pending(message.Id, payloadProtector.Protect(message, expiry), expiry));

        // A duplicate logical id fails here on the primary key instead of overwriting a queued email.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
