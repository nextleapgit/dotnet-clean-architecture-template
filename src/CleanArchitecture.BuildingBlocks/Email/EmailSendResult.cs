namespace CleanArchitecture.BuildingBlocks.Email;

public enum EmailSendOutcome
{
    Accepted = 0,
    TemporaryFailure = 1,
    PermanentFailure = 2
}

/// <summary>The outcome of one send attempt. <see cref="ErrorCode"/> is always one of <see cref="EmailErrorCodes"/>.</summary>
public sealed record EmailSendResult(EmailSendOutcome Outcome, string? ErrorCode)
{
    public static readonly EmailSendResult Accepted = new(EmailSendOutcome.Accepted, null);

    public static EmailSendResult TemporaryFailure(string errorCode) => new(EmailSendOutcome.TemporaryFailure, errorCode);

    public static EmailSendResult PermanentFailure(string errorCode) => new(EmailSendOutcome.PermanentFailure, errorCode);
}

/// <summary>
/// The only values ever stored or logged for a failed email. Never record raw SMTP responses,
/// exception text, recipients, or bodies.
/// </summary>
public static class EmailErrorCodes
{
    public const string SmtpTemporaryFailure = "smtp_temporary_failure";
    public const string SmtpConnectionFailure = "smtp_connection_failure";
    public const string SmtpTimeout = "smtp_timeout";
    public const string SmtpPermanentFailure = "smtp_permanent_failure";
    public const string PayloadProtectionFailure = "payload_protection_failure";
    public const string DeliveryWindowElapsed = "delivery_window_elapsed";
    public const string AttemptsExhausted = "attempts_exhausted";
}
