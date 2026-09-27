using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users;

/// <summary>The raw token goes into an email and nowhere else; only its hash is stored.</summary>
internal sealed record IssuedUserToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Issues and redeems single-use emailed tokens. Only the newest token of a purpose is usable.</summary>
internal sealed class UserTokenIssuer(
    IUserTokenStore userTokenStore,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<IssuedUserToken> IssueAsync(
        Guid userId,
        UserTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken,
        string? payload = null)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;

        foreach (UserToken previous in await userTokenStore.GetUsableAsync(userId, purpose, utcNow, cancellationToken))
        {
            previous.Consume(utcNow);
        }

        string token = tokenProvider.GenerateOpaqueToken();
        var userToken = UserToken.Issue(userId, purpose, tokenProvider.HashOpaqueToken(token), utcNow, lifetime, payload);

        userTokenStore.Add(userToken);

        return new IssuedUserToken(token, userToken.ExpiresAtUtc);
    }

    /// <summary>The token with its user, or null when it is unknown, used, superseded, or expired.</summary>
    public async Task<UserToken?> FindUsableAsync(string token, UserTokenPurpose purpose, CancellationToken cancellationToken)
    {
        UserToken? userToken = await userTokenStore.FindByHashAsync(
            tokenProvider.HashOpaqueToken(token),
            purpose,
            cancellationToken);

        return userToken is not null && userToken.IsUsable(dateTimeProvider.UtcNow) ? userToken : null;
    }
}
