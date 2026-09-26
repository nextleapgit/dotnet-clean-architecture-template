using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    string CreateAccessToken(User user);

    string GenerateRefreshToken();

    /// <summary>Only this hash is persisted, so a database leak does not expose usable tokens.</summary>
    string HashRefreshToken(string refreshToken);
}
