using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.IntegrationTests.Todos;

namespace CleanArchitecture.IntegrationTests.Tenancy;

/// <summary>Every account is created in its own tenant unless a test says otherwise.</summary>
public sealed class TenantIsolationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, Guid TenantId, string Email);

    private sealed record UserPageDto(List<UserDto> Items, int TotalCount);

    [Fact]
    public async Task OtherTenant_Should_NotReadOrModifyTodos()
    {
        // Arrange
        Account owner = await CreateAccountAsync();
        Authenticate(owner.Tokens.AccessToken);
        Guid todoId = await TodoApi.CreateAsync(HttpClient, "Tenant A secret", CancellationToken);

        Account intruder = await CreateAccountAsync();
        HttpClient intruderClient = CreateClient();
        Authenticate(intruderClient, intruder.Tokens.AccessToken);

        // Act
        HttpStatusCode get = (await intruderClient.GetAsync($"todos/{todoId}", CancellationToken)).StatusCode;
        HttpStatusCode complete = (await intruderClient.PutAsync($"todos/{todoId}/complete", null, CancellationToken)).StatusCode;
        HttpStatusCode copy = (await intruderClient.PostAsync($"todos/{todoId}/copy", null, CancellationToken)).StatusCode;
        HttpStatusCode delete = (await intruderClient.DeleteAsync($"todos/{todoId}", CancellationToken)).StatusCode;
        List<TodosTests.TodoDto>? intruderTodos =
            await intruderClient.GetFromJsonAsync<List<TodosTests.TodoDto>>("todos", CancellationToken);

        // Assert
        get.ShouldBe(HttpStatusCode.NotFound);
        complete.ShouldBe(HttpStatusCode.NotFound);
        copy.ShouldBe(HttpStatusCode.NotFound);
        delete.ShouldBe(HttpStatusCode.NotFound);
        intruderTodos.ShouldBeEmpty();
        (await TodoApi.GetAsync(HttpClient, todoId, CancellationToken)).IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task OtherTenantsManager_Should_NotSeeOrManageUsers()
    {
        // Arrange
        Account victim = await CreateAccountAsync();
        Account intruder = await CreateAccountAsync(Role.Manager);
        Authenticate(intruder.Tokens.AccessToken);

        // Act
        HttpStatusCode get = (await HttpClient.GetAsync($"users/{victim.UserId}", CancellationToken)).StatusCode;
        HttpStatusCode rename = (await HttpClient.PutAsJsonAsync(
            $"users/{victim.UserId}",
            new { firstName = "Hacked", lastName = "User" },
            CancellationToken)).StatusCode;
        HttpStatusCode promote = (await HttpClient.PutAsJsonAsync(
            $"users/{victim.UserId}/role",
            new { role = Role.Manager },
            CancellationToken)).StatusCode;
        HttpStatusCode deactivate = (await HttpClient.PutAsync($"users/{victim.UserId}/deactivate", null, CancellationToken)).StatusCode;

        // Assert
        get.ShouldBe(HttpStatusCode.NotFound);
        rename.ShouldBe(HttpStatusCode.NotFound);
        promote.ShouldBe(HttpStatusCode.NotFound);
        deactivate.ShouldBe(HttpStatusCode.NotFound);
        (await LoginAsync(victim.Email)).AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UserList_Should_NotIncludeOtherTenantsUsers()
    {
        // Arrange
        Account owner = await CreateAccountAsync();
        Account intruder = await CreateAccountAsync(Role.Manager);
        Authenticate(intruder.Tokens.AccessToken);

        // Act
        UserPageDto? page = await HttpClient.GetFromJsonAsync<UserPageDto>("users?pageSize=100", CancellationToken);

        // Assert
        page!.Items.ShouldNotContain(u => u.Id == owner.UserId);
        page.Items.ShouldAllBe(u => u.TenantId == intruder.TenantId);
        page.TotalCount.ShouldBe(1);
    }
}
