using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, Guid TenantId, string Email, string FirstName, string LastName, Role Role, bool IsActive);

    private sealed record UserPageDto(List<UserDto> Items, int Page, int PageSize, int TotalCount);

    [Fact]
    public async Task CreateUser_Should_ReturnConflict_WhenEmailDiffersOnlyByCase()
    {
        // Arrange
        string email = UniqueEmail();
        await CreateUserAsync(email);
        Account manager = await CreateAccountAsync(Role.Manager);
        Authenticate(manager.Tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users",
            new { email = email.ToUpperInvariant(), firstName = "Test", lastName = "User", password = Password, role = Role.Member },
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
            await CreateUserAsync(email);
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
    public async Task Register_Should_NoLongerExist() =>
        (await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email = UniqueEmail(), firstName = "Test", lastName = "User", password = Password },
            CancellationToken))
        .StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);

    [Fact]
    public async Task GetUser_Should_ReturnUnauthorized_WhenTokenIsMissing() =>
        (await HttpClient.GetAsync($"users/{Guid.NewGuid()}", CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task GetMe_Should_ReturnProfileOfSignedInUser()
    {
        // Arrange
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        UserDto? me = await HttpClient.GetFromJsonAsync<UserDto>("users/me", CancellationToken);

        // Assert
        me!.Id.ShouldBe(account.UserId);
        me.TenantId.ShouldBe(account.TenantId);
        me.Email.ShouldBe(account.Email);
        me.Role.ShouldBe(Role.Member);
        me.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUsers_Should_ReturnPagedUsersOfCurrentTenant_WhenCallerIsManager()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Guid memberId = await CreateUserAsync(UniqueEmail(), Role.Member, manager.TenantId);
        Authenticate(manager.Tokens.AccessToken);

        // Act
        UserPageDto? page = await HttpClient.GetFromJsonAsync<UserPageDto>("users?page=1&pageSize=10", CancellationToken);

        // Assert
        page!.Page.ShouldBe(1);
        page.PageSize.ShouldBe(10);
        page.TotalCount.ShouldBe(2);
        page.Items.Select(u => u.Id).ShouldBe([manager.UserId, memberId], ignoreOrder: true);
    }

    [Theory]
    [InlineData("users?page=0")]
    [InlineData("users?pageSize=101")]
    public async Task GetUsers_Should_ReturnBadRequest_WhenPagingIsOutOfRange(string route)
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Authenticate(manager.Tokens.AccessToken);

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
