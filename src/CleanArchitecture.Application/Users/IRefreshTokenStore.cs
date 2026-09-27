using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users;

public interface IRefreshTokenStore
{
    /// <summary>Finds a token (active or not) by its hash, including its user.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(
        Guid familyId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    /// <summary>Every active session of the user, tracked so they can be revoked.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    void Add(RefreshToken refreshToken);
}
