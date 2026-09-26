namespace CleanArchitecture.Infrastructure.Email;

internal sealed class EmailOutboxMessage
{
    public const int ErrorCodeMaxLength = 64;

    private EmailOutboxMessage()
    {
    }

    /// <summary>The logical email id, stable across retries.</summary>
    public Guid Id { get; init; }
    public EmailOutboxStatus Status { get; init; }

    /// <summary>Protected recipient, subject, and bodies. Present only while Pending or Processing.</summary>
    public byte[]? Payload { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime NextAttemptAtUtc { get; init; }
    public int AttemptCount { get; init; }
    public Guid? LeaseId { get; init; }
    public DateTime? LeaseExpiresAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; init; }
    public string? ErrorCode { get; init; }

    // Timestamps are assigned by the database clock (column defaults), the authority for all instances.
    public static EmailOutboxMessage Pending(Guid id, byte[] payload, DateTime expiresAtUtc) =>
        new()
        {
            Id = id,
            Status = EmailOutboxStatus.Pending,
            Payload = payload,
            ExpiresAtUtc = expiresAtUtc
        };
}
