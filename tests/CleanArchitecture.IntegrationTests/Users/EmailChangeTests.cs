using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.IntegrationTests.Todos;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class EmailChangeTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string ConfirmationSubject = "Confirm your new email address";
    private const string NoticeSubject = "Your email address is about to change";

    private sealed record MeDto(Guid Id, string Email);

    private Task<HttpResponseMessage> RequestAsync(string newEmail, string currentPassword = Password) =>
        HttpClient.PutAsJsonAsync("users/me/email", new { currentPassword, newEmail }, CancellationToken);

    private Task<HttpResponseMessage> ConfirmAsync(string token) =>
        HttpClient.PostAsJsonAsync("users/email/confirm", new { token }, CancellationToken);

    private Task<HttpResponseMessage> LoginResponseAsync(string email) =>
        HttpClient.PostAsJsonAsync("users/login", new { email, password = Password }, CancellationToken);

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemBody>(CancellationToken))?.Code;

    [Fact]
    public async Task EmailChange_Should_TakeEffectOnlyOnceTheNewAddressIsConfirmed()
    {
        // Arrange
        Account account = await CreateAccountAsync();
        string newEmail = UniqueEmail();
        Authenticate(account.Tokens.AccessToken);

        // Act: request — nothing changes yet, the current address is told.
        (await RequestAsync(newEmail)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await ReadEmailsAsync(account.Email, NoticeSubject)).ShouldHaveSingleItem();
        (await LoginResponseAsync(account.Email)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act: confirm with the link sent to the new address — anonymously, as from a mail client.
        string token = await ReadEmailedTokenAsync(newEmail, ConfirmationSubject);
        HttpClient.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage confirmed = await ConfirmAsync(token);

        // Assert: the new address signs in, the old one does not, and the old session has ended.
        confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginResponseAsync(account.Email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        AccessTokens tokens = await LoginAsync(newEmail);
        Authenticate(tokens.AccessToken);
        (await HttpClient.GetFromJsonAsync<MeDto>("users/me", CancellationToken))!.Email.ShouldBe(newEmail);

        Authenticate(account.Tokens.AccessToken);
        (await HttpClient.GetAsync("users/me", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        HttpClient.DefaultRequestHeaders.Authorization = null;
        (await ProblemCodeAsync(await ConfirmAsync(token))).ShouldBe("Users.InvalidOrExpiredToken");
    }

    [Fact]
    public async Task Request_Should_NotRevealThatAnAddressBelongsToAnotherAccount()
    {
        // Arrange: the other account lives in another tenant.
        Account account = await CreateAccountAsync();
        Account other = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        HttpResponseMessage response = await RequestAsync(other.Email.ToUpperInvariant());

        // Assert: the same answer as for a free address, but the other account's mailbox gets no link.
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await ReadEmailsAsync(other.Email, ConfirmationSubject)).ShouldBeEmpty();
        (await ReadEmailsAsync(account.Email, NoticeSubject)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Confirm_Should_ReturnConflict_WhenTheAddressWasTakenMeanwhile()
    {
        // Arrange
        Account account = await CreateAccountAsync();
        string newEmail = UniqueEmail();
        Authenticate(account.Tokens.AccessToken);
        (await RequestAsync(newEmail)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string token = await ReadEmailedTokenAsync(newEmail, ConfirmationSubject);
        await InviteUserAsync(newEmail);

        // Act
        HttpResponseMessage confirmed = await ConfirmAsync(token);

        // Assert
        confirmed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(confirmed)).ShouldBe("Users.EmailNotUnique");
        (await LoginResponseAsync(account.Email)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_Should_RequireTheCurrentPassword()
    {
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);

        HttpResponseMessage response = await RequestAsync(UniqueEmail(), currentPassword: "WrongPassword1");

        (await ProblemCodeAsync(response)).ShouldBe("Users.InvalidCurrentPassword");
    }
}
