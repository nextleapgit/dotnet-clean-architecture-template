using System.Reflection;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class InMemoryUserTokenStore(InMemoryUserStore userStore) : IUserTokenStore
{
    private static readonly PropertyInfo UserProperty = typeof(UserToken).GetProperty(nameof(UserToken.User))!;

    public List<UserToken> Tokens { get; } = [];

    public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Populates <see cref="UserToken.User"/>, as the EF include does.</summary>
    public Task<UserToken?> FindByHashAsync(string tokenHash, UserTokenPurpose purpose, CancellationToken cancellationToken)
    {
        UserToken? token = Tokens.SingleOrDefault(t => t.TokenHash == tokenHash && t.Purpose == purpose);

        if (token is not null)
        {
            UserProperty.SetValue(token, userStore.Users.Single(u => u.Id == token.UserId));
        }

        return Task.FromResult(token);
    }

    public Task<IReadOnlyList<UserToken>> GetUsableAsync(
        Guid userId,
        UserTokenPurpose purpose,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserToken>>(
            Tokens.Where(t => t.UserId == userId && t.Purpose == purpose && t.IsUsable(utcNow)).ToList());

    public void Add(UserToken userToken) => Tokens.Add(userToken);
}
