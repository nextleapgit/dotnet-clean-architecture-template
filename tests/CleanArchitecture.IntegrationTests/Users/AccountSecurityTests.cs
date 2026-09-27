using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.IntegrationTests.Todos;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class AccountSecurityTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string NewPassword = "BrandNew123";

    private sealed record MeDto(Guid Id, Guid TenantId, Role Role, bool InvitationPending);

    private Task<HttpResponseMessage> LoginResponseAsync(string email, string password) =>
        HttpClient.PostAsJsonAsync("users/login", new { email, password }, CancellationToken);

    private Task<HttpResponseMessage> AcceptAsync(string token, string password) =>
        HttpClient.PostAsJsonAsync("users/invitations/accept", new { token, password }, CancellationToken);

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken }, CancellationToken);

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemBody>(CancellationToken))?.Code;

    [Fact]
    public async Task Invitation_Should_LetTheInvitedUserChooseTheirPassword_Once()
    {
        // Arrange
        string email = UniqueEmail();
        await InviteUserAsync(email);
        string token = await ReadEmailedTokenAsync(email, InvitationSubject);

        // Act + Assert: no password exists before the invitation is accepted.
        (await LoginResponseAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AcceptAsync(token, Password)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginResponseAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ProblemCodeAsync(await AcceptAsync(token, "Another123"))).ShouldBe("Users.InvalidOrExpiredToken");
    }

    [Fact]
    public async Task ResendInvitation_Should_ReplaceThePreviousLink()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Authenticate(manager.Tokens.AccessToken);
        string email = UniqueEmail();
        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            "users",
            new { email, firstName = "New", lastName = "User", role = Role.Member },
            CancellationToken);
        Guid userId = await created.Content.ReadFromJsonAsync<Guid>(CancellationToken);
        string first = await ReadEmailedTokenAsync(email, InvitationSubject);

        // Act
        HttpResponseMessage resend = await HttpClient.PostAsync($"users/{userId}/invitation", null, CancellationToken);
        string second = await ReadEmailedTokenAsync(email, InvitationSubject);

        // Assert
        resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.ShouldNotBe(first);
        (await ProblemCodeAsync(await AcceptAsync(first, Password))).ShouldBe("Users.InvalidOrExpiredToken");
        (await AcceptAsync(second, Password)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ProblemCodeAsync(await HttpClient.PostAsync($"users/{userId}/invitation", null, CancellationToken)))
            .ShouldBe("Users.InvitationAlreadyAccepted");
    }

    [Fact]
    public async Task ForgotAndResetPassword_Should_ReplaceThePasswordAndEndEverySession()
    {
        // Arrange
        Account account = await CreateAccountAsync();

        // Act
        HttpResponseMessage unknown = await HttpClient.PostAsJsonAsync("users/password/forgot", new { email = UniqueEmail() }, CancellationToken);
        HttpResponseMessage forgot = await HttpClient.PostAsJsonAsync("users/password/forgot", new { email = account.Email }, CancellationToken);
        string token = await ReadEmailedTokenAsync(account.Email, PasswordResetSubject);
        HttpResponseMessage reset = await HttpClient.PostAsJsonAsync(
            "users/password/reset",
            new { token, newPassword = NewPassword },
            CancellationToken);

        // Assert: both requests look the same, whether or not the account exists.
        unknown.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginResponseAsync(account.Email, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginResponseAsync(account.Email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RefreshAsync(account.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(await HttpClient.PostAsJsonAsync(
            "users/password/reset",
            new { token, newPassword = "Again12345" },
            CancellationToken))).ShouldBe("Users.InvalidOrExpiredToken");
    }

    [Fact]
    public async Task ChangePassword_Should_KeepTheCurrentSessionAndEndTheOthers()
    {
        // Arrange: two sessions of the same user.
        Account account = await CreateAccountAsync();
        AccessTokens otherSession = await LoginAsync(account.Email);
        Authenticate(account.Tokens.AccessToken);

        // Act
        HttpResponseMessage wrong = await HttpClient.PutAsJsonAsync(
            "users/me/password",
            new { currentPassword = "WrongPassword1", newPassword = NewPassword },
            CancellationToken);
        HttpResponseMessage change = await HttpClient.PutAsJsonAsync(
            "users/me/password",
            new { currentPassword = Password, newPassword = NewPassword },
            CancellationToken);

        // Assert
        (await ProblemCodeAsync(wrong)).ShouldBe("Users.InvalidCurrentPassword");
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RefreshAsync(account.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RefreshAsync(otherSession.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LoginResponseAsync(account.Email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RepeatedWrongPasswords_Should_LockTheAccount_UntilAManagerUnlocksIt()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Account member = await CreateAccountAsync(Role.Member, manager.TenantId);

        // Act
        for (int attempt = 0; attempt < 5; attempt++)
        {
            (await LoginResponseAsync(member.Email, "WrongPassword1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        HttpResponseMessage locked = await LoginResponseAsync(member.Email, Password);

        HttpClient managerClient = CreateClient();
        Authenticate(managerClient, manager.Tokens.AccessToken);
        HttpResponseMessage unlock = await managerClient.PutAsync($"users/{member.UserId}/unlock", null, CancellationToken);

        // Assert
        locked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(locked)).ShouldBe("Users.LockedOut");
        unlock.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginResponseAsync(member.Email, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_Should_AppointAnotherAdmin_OnlyInThePlatformTenant()
    {
        // Arrange
        using HttpClient admin = await CreateAdminClientAsync();
        MeDto me = (await admin.GetFromJsonAsync<MeDto>("users/me", CancellationToken))!;
        Guid customerTenant = await CreateTenantAsync();
        string email = UniqueEmail();

        // Act
        HttpResponseMessage inCustomerTenant = await admin.PostAsJsonAsync(
            $"tenants/{customerTenant}/users",
            new { email = UniqueEmail(), firstName = "Not", lastName = "Allowed", role = Role.Admin },
            CancellationToken);
        await CreateUserAsync(email, Role.Admin, me.TenantId);
        AccessTokens secondAdmin = await LoginAsync(email);
        HttpClient secondAdminClient = CreateClient();
        Authenticate(secondAdminClient, secondAdmin.AccessToken);

        // Assert
        (await ProblemCodeAsync(inCustomerTenant)).ShouldBe("Users.AdminRoleNotAssignable");
        (await secondAdminClient.GetAsync("tenants", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await secondAdminClient.GetAsync($"tenants/{customerTenant}/users", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvitedUser_Should_BeListedAsPending_UntilTheyAccept()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        string email = UniqueEmail();
        Guid invitedId = await InviteUserAsync(email, Role.Member, manager.TenantId);
        Authenticate(manager.Tokens.AccessToken);

        // Act
        MeDto invited = (await HttpClient.GetFromJsonAsync<MeDto>($"users/{invitedId}", CancellationToken))!;

        // Assert
        invited.InvitationPending.ShouldBeTrue();
    }

    [Fact]
    public async Task ParallelWrongPasswords_Should_StillLockTheAccount()
    {
        // Arrange
        Account member = await CreateAccountAsync();

        // Act: more guesses than allowed, all at once — none may lose its failed-attempt count.
        HttpResponseMessage[] guesses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => LoginResponseAsync(member.Email, "WrongPassword1")));

        HttpResponseMessage withRightPassword = await LoginResponseAsync(member.Email, Password);

        // Assert
        guesses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Unauthorized || r.StatusCode == HttpStatusCode.Forbidden);
        withRightPassword.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(withRightPassword)).ShouldBe("Users.LockedOut");
    }

    [Fact]
    public async Task Manager_Should_NotUnlockOrReinvite_AnotherTenantsUser_OrThemselves()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Account otherTenantsMember = await CreateAccountAsync();
        Guid otherTenantsInvitee = await InviteUserAsync(UniqueEmail());
        Authenticate(manager.Tokens.AccessToken);

        // Act
        HttpResponseMessage unlockOther = await HttpClient.PutAsync($"users/{otherTenantsMember.UserId}/unlock", null, CancellationToken);
        HttpResponseMessage reinviteOther = await HttpClient.PostAsync($"users/{otherTenantsInvitee}/invitation", null, CancellationToken);
        HttpResponseMessage unlockSelf = await HttpClient.PutAsync($"users/{manager.UserId}/unlock", null, CancellationToken);
        HttpResponseMessage reinviteSelf = await HttpClient.PostAsync($"users/{manager.UserId}/invitation", null, CancellationToken);

        // Assert: another tenant's users do not exist for the manager.
        unlockOther.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        reinviteOther.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(unlockSelf)).ShouldBe("Users.CannotChangeOwnAccess");
        (await ProblemCodeAsync(reinviteSelf)).ShouldBe("Users.CannotChangeOwnAccess");
    }

    [Fact]
    public async Task InvitationLink_Should_WorkOnlyOnce_WhenUsedTwiceAtTheSameMoment()
    {
        // Arrange
        string email = UniqueEmail();
        await InviteUserAsync(email);
        string token = await ReadEmailedTokenAsync(email, InvitationSubject);

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(AcceptAsync(token, Password), AcceptAsync(token, NewPassword));

        // Assert: one wins; the other is rejected as a used link (400) or a lost race (409).
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.ShouldContain(r => r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.Conflict);

        string winner = responses[0].StatusCode == HttpStatusCode.NoContent ? Password : NewPassword;
        (await LoginResponseAsync(email, winner)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_Should_TakeAtLeastTheMinimumTime_ForAnUnknownEmail()
    {
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/password/forgot",
            new { email = UniqueEmail() },
            CancellationToken);
        stopwatch.Stop();

        // Assert: the unknown email answers no faster than a real one would.
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(950));
    }
}
