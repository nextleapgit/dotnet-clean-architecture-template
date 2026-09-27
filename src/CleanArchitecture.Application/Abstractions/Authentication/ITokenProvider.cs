using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    /// <param name="sessionId">The refresh-token family the access token belongs to.</param>
    string CreateAccessToken(User user, Guid sessionId);

    /// <summary>A random, URL-safe token with 256 bits of entropy (refresh tokens, emailed links).</summary>
    string GenerateOpaqueToken();

    /// <summary>Only this hash is persisted, so a database leak does not expose usable tokens.</summary>
    string HashOpaqueToken(string token);
}
