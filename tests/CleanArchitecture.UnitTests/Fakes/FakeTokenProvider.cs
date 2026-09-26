using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.UnitTests.Fakes;

/// <summary>Deterministic tokens; the "hash" is recognizable so tests can assert only hashes are stored.</summary>
public sealed class FakeTokenProvider : ITokenProvider
{
    private int _counter;

    public string CreateAccessToken(User user) => $"access-{user.Id}";

    public string GenerateRefreshToken() => $"refresh-{++_counter}";

    public string HashRefreshToken(string refreshToken) => $"hash({refreshToken})";
}
