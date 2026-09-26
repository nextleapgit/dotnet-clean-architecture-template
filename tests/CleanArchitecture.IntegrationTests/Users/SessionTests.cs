using System.Net;
using System.Net.Http.Json;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class SessionTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken }, CancellationToken);

    [Fact]
    public async Task Refresh_Should_RotateTokens()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();

        // Act
        HttpResponseMessage response = await RefreshAsync(account.Tokens.RefreshToken);

        // Assert
        response.EnsureSuccessStatusCode();
        AccessTokens rotated = (await response.Content.ReadFromJsonAsync<AccessTokens>(CancellationToken))!;
        rotated.RefreshToken.ShouldNotBe(account.Tokens.RefreshToken);
        (await RefreshAsync(rotated.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_Should_ReturnBadRequest_WhenTokenIsUnknown() =>
        (await RefreshAsync("this-token-does-not-exist")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Refresh_Should_RevokeWholeFamily_WhenRotatedTokenIsReused()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        HttpResponseMessage firstRotation = await RefreshAsync(account.Tokens.RefreshToken);
        AccessTokens rotated = (await firstRotation.Content.ReadFromJsonAsync<AccessTokens>(CancellationToken))!;

        // Act: an attacker replays the original token.
        HttpResponseMessage replay = await RefreshAsync(account.Tokens.RefreshToken);

        // Assert: the replay fails and the legitimate successor is revoked too.
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RefreshAsync(rotated.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_Should_SucceedOnlyOnce_WhenTheSameTokenIsUsedConcurrently()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            RefreshAsync(account.Tokens.RefreshToken),
            RefreshAsync(account.Tokens.RefreshToken));

        // Assert
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK
            || r.StatusCode == HttpStatusCode.BadRequest
            || r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Logout_Should_RevokeTheSession()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        HttpResponseMessage logout = await HttpClient.PostAsJsonAsync(
            "users/logout",
            new { refreshToken = account.Tokens.RefreshToken },
            CancellationToken);

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RefreshAsync(account.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_Should_NotRevokeAnotherUsersSession()
    {
        // Arrange
        Account victim = await RegisterAndLoginAsync();
        Account attacker = await RegisterAndLoginAsync();
        Authenticate(attacker.Tokens.AccessToken);

        // Act
        HttpResponseMessage logout = await HttpClient.PostAsJsonAsync(
            "users/logout",
            new { refreshToken = victim.Tokens.RefreshToken },
            CancellationToken);

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RefreshAsync(victim.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_Should_RequireAuthentication() =>
        (await HttpClient.PostAsJsonAsync("users/logout", new { refreshToken = "x" }, CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
