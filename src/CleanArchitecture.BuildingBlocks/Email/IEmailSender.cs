namespace CleanArchitecture.BuildingBlocks.Email;

/// <summary>
/// Makes exactly one delivery attempt, without internal retries. Only the outbox worker calls it.
/// Acceptance means the SMTP server accepted the message, not that it reached an inbox.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
