using System.Net;
using System.Net.Http.Json;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, Guid TenantId, string Email, string FirstName, string LastName);

    private sealed record UserPageDto(List<UserDto> Items, int Page, int PageSize, int TotalCount);

    [Fact]
    public async Task Register_Should_ReturnConflict_WhenEmailDiffersOnlyByCase()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email = email.ToUpperInvariant(), firstName = "Test", lastName = "User", password = Password },
            CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_Should_ReturnUnauthorized_WhenCredentialsAreWrong(bool userExists)
    {
        // Arrange
        string email = UniqueEmail();
        if (userExists)
        {
            await RegisterUserAsync(email);
        }

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = "WrongPassword1" },
            CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUser_Should_ReturnUnauthorized_WhenTokenIsMissing() =>
        (await HttpClient.GetAsync($"users/{Guid.NewGuid()}", CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task GetMe_Should_ReturnProfileOfSignedInUser()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        UserDto? me = await HttpClient.GetFromJsonAsync<UserDto>("users/me", CancellationToken);

        // Assert
        me!.Id.ShouldBe(account.UserId);
        me.Email.ShouldBe(account.Email);
        me.FirstName.ShouldBe("Test");
    }

    [Fact]
    public async Task GetUsers_Should_ReturnPagedUsersOfCurrentTenant()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        UserPageDto? page = await HttpClient.GetFromJsonAsync<UserPageDto>("users?page=1&pageSize=10", CancellationToken);

        // Assert
        page!.Page.ShouldBe(1);
        page.PageSize.ShouldBe(10);
        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(account.UserId);
    }

    [Theory]
    [InlineData("users?page=0")]
    [InlineData("users?pageSize=101")]
    public async Task GetUsers_Should_ReturnBadRequest_WhenPagingIsOutOfRange(string route)
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(route, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("users")]
    [InlineData("users/me")]
    public async Task UserReads_Should_ReturnUnauthorized_WhenTokenIsMissing(string route) =>
        (await HttpClient.GetAsync(route, CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
