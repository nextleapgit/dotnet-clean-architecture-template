namespace CleanArchitecture.BuildingBlocks.Email;

/// <summary>
/// Durably enqueues an email in the same database transaction as the state change that caused it.
/// Use cases call this — never <see cref="IEmailSender"/>. A background worker sends it later.
/// </summary>
public interface IEmailOutbox
{
    /// <param name="expiresAtUtc">
    /// When the email becomes pointless to send. Bound it by the validity of any link or token it carries.
    /// </param>
    /// <exception cref="InvalidOperationException">No transaction is active on the current unit of work.</exception>
    Task EnqueueAsync(EmailMessage message, DateTime expiresAtUtc, CancellationToken cancellationToken);
}
