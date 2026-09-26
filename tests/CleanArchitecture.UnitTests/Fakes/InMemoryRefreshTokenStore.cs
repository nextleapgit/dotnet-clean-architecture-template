using System.Reflection;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private static readonly PropertyInfo UserProperty = typeof(RefreshToken).GetProperty(nameof(RefreshToken.User))!;

    private readonly Dictionary<Guid, User> _users = [];

    public List<RefreshToken> Tokens { get; } = [];

    /// <summary>Makes the store populate <see cref="RefreshToken.User"/>, as the EF include does.</summary>
    public void KnowUser(User user) => _users[user.Id] = user;

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        RefreshToken? token = Tokens.SingleOrDefault(t => t.TokenHash == tokenHash);

        if (token is not null && _users.TryGetValue(token.UserId, out User? user))
        {
            UserProperty.SetValue(token, user);
        }

        return Task.FromResult(token);
    }

    public Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(
        Guid familyId,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            Tokens.Where(t => t.FamilyId == familyId && t.IsActive(utcNow)).ToList());

    public void Add(RefreshToken refreshToken) => Tokens.Add(refreshToken);
}
