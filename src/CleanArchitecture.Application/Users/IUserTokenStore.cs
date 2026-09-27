using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users;

public interface IUserTokenStore
{
    /// <summary>Finds a token (usable or not) by its hash and purpose, including its user.</summary>
    Task<UserToken?> FindByHashAsync(string tokenHash, UserTokenPurpose purpose, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserToken>> GetUsableAsync(
        Guid userId,
        UserTokenPurpose purpose,
        DateTime utcNow,
        CancellationToken cancellationToken);

    void Add(UserToken userToken);
}
