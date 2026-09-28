using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.IntegrationTests.Todos;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class UserAdministrationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, Guid TenantId, string Email, string FirstName, Role Role, bool IsActive);

    private sealed record UserPageDto(List<UserDto> Items, int TotalCount);

    private sealed record TenantDto(Guid Id, string Name, bool IsActive);

    private sealed record TenantPageDto(List<TenantDto> Items, int TotalCount);

    private static object NewUser(Role role = Role.Member) =>
        new { email = UniqueEmail(), firstName = "New", lastName = "User", role };

    [Fact]
    public async Task Admin_Should_CreateTenantWithAManager_WhoManagesTheTenantsUsers()
    {
        // Arrange: the admin creates a tenant and its first manager.
        string tenantName = $"Tenant {Guid.NewGuid():N}";
        Guid tenantId = await CreateTenantAsync(tenantName);
        Account manager = await CreateAccountAsync(Role.Manager, tenantId);
        Authenticate(manager.Tokens.AccessToken);

        // Act: the manager creates, promotes, and renames a user.
        HttpResponseMessage create = await HttpClient.PostAsJsonAsync("users", NewUser(), CancellationToken);
        Guid userId = await create.Content.ReadFromJsonAsync<Guid>(CancellationToken);
        HttpResponseMessage promote = await HttpClient.PutAsJsonAsync($"users/{userId}/role", new { role = Role.Manager }, CancellationToken);
        HttpResponseMessage rename = await HttpClient.PutAsJsonAsync($"users/{userId}", new { firstName = "Renamed", lastName = "User" }, CancellationToken);

        // Assert
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        promote.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        rename.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        UserDto user = (await HttpClient.GetFromJsonAsync<UserDto>($"users/{userId}", CancellationToken))!;
        user.TenantId.ShouldBe(tenantId);
        user.Role.ShouldBe(Role.Manager);
        user.FirstName.ShouldBe("Renamed");

        using HttpClient admin = await CreateAdminClientAsync();
        TenantDto tenant = (await admin.GetFromJsonAsync<TenantDto>($"tenants/{tenantId}", CancellationToken))!;
        tenant.Name.ShouldBe(tenantName);
        UserPageDto tenantUsers = (await admin.GetFromJsonAsync<UserPageDto>($"tenants/{tenantId}/users", CancellationToken))!;
        tenantUsers.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Admin_Should_ManageUsersOfAnyTenant()
    {
        // Arrange
        Account member = await CreateAccountAsync();
        using HttpClient admin = await CreateAdminClientAsync();

        // Act
        HttpResponseMessage promote = await admin.PutAsJsonAsync(
            $"tenants/{member.TenantId}/users/{member.UserId}/role",
            new { role = Role.Manager },
            CancellationToken);

        // Assert
        promote.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        UserDto user = (await admin.GetFromJsonAsync<UserDto>(
            $"tenants/{member.TenantId}/users/{member.UserId}",
            CancellationToken))!;
        user.Role.ShouldBe(Role.Manager);
    }

    [Fact]
    public async Task Admin_Should_ListTenants()
    {
        // Arrange
        await CreateTenantAsync();
        using HttpClient admin = await CreateAdminClientAsync();

        // Act
        TenantPageDto page = (await admin.GetFromJsonAsync<TenantPageDto>("tenants?pageSize=5", CancellationToken))!;

        // Assert
        page.Items.Count.ShouldBeInRange(1, 5);
        page.TotalCount.ShouldBeGreaterThanOrEqualTo(2); // the platform tenant plus the one just created
    }

    [Fact]
    public async Task Member_Should_OnlyReachTheirOwnProfile()
    {
        // Arrange
        Account member = await CreateAccountAsync();
        Authenticate(member.Tokens.AccessToken);

        // Act + Assert
        (await HttpClient.GetAsync("users/me", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync("users", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.GetAsync($"users/{member.UserId}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.PostAsJsonAsync("users", NewUser(), CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.GetAsync("tenants", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_Should_BeForbiddenFromTenantAdministration()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Authenticate(manager.Tokens.AccessToken);

        // Act + Assert
        (await HttpClient.GetAsync("tenants", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.PostAsJsonAsync("tenants", new { name = "Nope" }, CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.GetAsync($"tenants/{manager.TenantId}/users", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.PutAsync($"tenants/{manager.TenantId}/deactivate", null, CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_Should_NotAssignAdminRoleOrChangeTheirOwnAccess()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Guid memberId = await CreateUserAsync(UniqueEmail(), Role.Member, manager.TenantId);
        Authenticate(manager.Tokens.AccessToken);

        // Act
        HttpResponseMessage toAdmin = await HttpClient.PutAsJsonAsync($"users/{memberId}/role", new { role = Role.Admin }, CancellationToken);
        HttpResponseMessage createAdmin = await HttpClient.PostAsJsonAsync("users", NewUser(Role.Admin), CancellationToken);
        (await ProblemCodeAsync(toAdmin)).ShouldBe("Users.AdminRoleNotAssignable");
        HttpResponseMessage demoteSelf = await HttpClient.PutAsJsonAsync($"users/{manager.UserId}/role", new { role = Role.Member }, CancellationToken);
        HttpResponseMessage deactivateSelf = await HttpClient.PutAsync($"users/{manager.UserId}/deactivate", null, CancellationToken);

        // Assert
        toAdmin.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        createAdmin.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(demoteSelf)).ShouldBe("Users.CannotChangeOwnAccess");
        (await ProblemCodeAsync(deactivateSelf)).ShouldBe("Users.CannotChangeOwnAccess");
    }

    [Fact]
    public async Task DeactivatedUser_Should_LoseAccessImmediately_AndRegainItWhenActivated()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Account member = await CreateAccountAsync(Role.Member, manager.TenantId);
        HttpClient memberClient = CreateClient();
        Authenticate(memberClient, member.Tokens.AccessToken);
        Authenticate(manager.Tokens.AccessToken);

        // Act
        HttpResponseMessage deactivate = await HttpClient.PutAsync($"users/{member.UserId}/deactivate", null, CancellationToken);

        // Assert: deactivation ended the member's sessions, so the unexpired access token no longer
        // authenticates (401), and no new session can be obtained.
        deactivate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await memberClient.GetAsync("users/me", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await TodoApiStatusAsync(memberClient)).ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(await LoginResponseAsync(member.Email))).ShouldBe("Users.AccountDisabled");
        (await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = member.Tokens.RefreshToken }, CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Act + Assert: activation restores access.
        (await HttpClient.PutAsync($"users/{member.UserId}/activate", null, CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginResponseAsync(member.Email)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivatedTenant_Should_BlockAllItsUsers_UntilActivated()
    {
        // Arrange
        Account manager = await CreateAccountAsync(Role.Manager);
        Authenticate(manager.Tokens.AccessToken);
        using HttpClient admin = await CreateAdminClientAsync();

        // Act
        HttpResponseMessage deactivate = await admin.PutAsync($"tenants/{manager.TenantId}/deactivate", null, CancellationToken);

        // Assert
        deactivate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.GetAsync("users", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(await LoginResponseAsync(manager.Email))).ShouldBe("Users.AccountDisabled");
        (await ProblemCodeAsync(await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = manager.Tokens.RefreshToken },
            CancellationToken))).ShouldBe("Users.AccountDisabled");

        // Act + Assert: activation restores access.
        (await admin.PutAsync($"tenants/{manager.TenantId}/activate", null, CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.GetAsync("users", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_Should_NotDeactivateTheirOwnTenantOrChangeTheirOwnAccess()
    {
        // Arrange
        using HttpClient admin = await CreateAdminClientAsync();
        UserDto me = (await admin.GetFromJsonAsync<UserDto>("users/me", CancellationToken))!;

        // Act
        HttpResponseMessage deactivateOwnTenant = await admin.PutAsync($"tenants/{me.TenantId}/deactivate", null, CancellationToken);
        HttpResponseMessage demoteSelf = await admin.PutAsJsonAsync($"users/{me.Id}/role", new { role = Role.Member }, CancellationToken);

        // Assert
        me.Role.ShouldBe(Role.Admin);
        (await ProblemCodeAsync(deactivateOwnTenant)).ShouldBe("Tenants.CannotDeactivateOwnTenant");
        (await ProblemCodeAsync(demoteSelf)).ShouldBe("Users.CannotChangeOwnAccess");
    }

    private Task<HttpResponseMessage> LoginResponseAsync(string email) =>
        HttpClient.PostAsJsonAsync("users/login", new { email, password = Password }, CancellationToken);

    private static async Task<HttpStatusCode> TodoApiStatusAsync(HttpClient client) =>
        (await client.GetAsync("todos", CancellationToken)).StatusCode;

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemBody>(CancellationToken))?.Code;
}
