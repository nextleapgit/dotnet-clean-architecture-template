namespace CleanArchitecture.Domain.Users;

/// <summary>
/// A refresh token. Only a hash of the token is stored. Tokens issued by rotation share a
/// <see cref="FamilyId"/>; presenting an already rotated token revokes the whole family.
/// </summary>
public sealed class RefreshToken
{
    public const int TokenHashLength = 64;

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

#pragma warning disable S1144 // The setter is used by EF Core to populate the navigation.
    public User User { get; private set; }
#pragma warning restore S1144

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime createdAtUtc, TimeSpan lifetime) =>
        Create(userId, tokenHash, Guid.CreateVersion7(), createdAtUtc, lifetime);

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresOnUtc > utcNow;

    public bool IsRevoked => RevokedAtUtc is not null;

    /// <summary>
    /// True once the token was exchanged for a successor. Presenting it again means it leaked —
    /// unlike a token revoked by logout, which is simply no longer valid.
    /// </summary>
    public bool WasRotated => ReplacedByTokenId is not null;

    /// <summary>Revokes this token and returns its successor in the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        RefreshToken successor = Create(UserId, newTokenHash, FamilyId, utcNow, lifetime);

        RevokedAtUtc = utcNow;
        ReplacedByTokenId = successor.Id;

        return successor;
    }

    public void Revoke(DateTime utcNow)
    {
        RevokedAtUtc ??= utcNow;
    }

    private static RefreshToken Create(
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTime createdAtUtc,
        TimeSpan lifetime) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAtUtc = createdAtUtc,
            ExpiresOnUtc = createdAtUtc.Add(lifetime)
        };
}
