using System.Net;
using System.Net.Http.Json;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
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
}
