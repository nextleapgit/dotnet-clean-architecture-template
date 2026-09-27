namespace CleanArchitecture.Domain.Users;

/// <summary>
/// A single-use token sent by email (invitation, password reset, email change). Only a hash of
/// the token is stored; the token itself exists only in the email.
/// </summary>
public sealed class UserToken
{
    public const int TokenHashLength = 64;

    private UserToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public UserTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; }

    /// <summary>Purpose-specific data, e.g. the new address of an email change.</summary>
    public string? Payload { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

#pragma warning disable S1144 // The setter is used by EF Core to populate the navigation.
    public User User { get; private set; }
#pragma warning restore S1144

    public static UserToken Issue(
        Guid userId,
        UserTokenPurpose purpose,
        string tokenHash,
        DateTime utcNow,
        TimeSpan lifetime,
        string? payload = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            Payload = payload,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime)
        };

    public bool IsUsable(DateTime utcNow) => ConsumedAtUtc is null && ExpiresAtUtc > utcNow;

    /// <summary>Marks the token as used — or, for a superseded token, as no longer usable.</summary>
    public void Consume(DateTime utcNow)
    {
        ConsumedAtUtc ??= utcNow;
    }
}
