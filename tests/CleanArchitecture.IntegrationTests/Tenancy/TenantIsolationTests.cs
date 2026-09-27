using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.IntegrationTests.Todos;

namespace CleanArchitecture.IntegrationTests.Tenancy;

/// <summary>Each registration creates its own tenant, so two accounts are two tenants.</summary>
public sealed class TenantIsolationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, Guid TenantId, string Email);

    private sealed record UserPageDto(List<UserDto> Items, int TotalCount);

    [Fact]
    public async Task OtherTenant_Should_NotReadOrModifyTodos()
    {
        // Arrange
        Account owner = await RegisterAndLoginAsync();
        Authenticate(owner.Tokens.AccessToken);
        Guid todoId = await TodoApi.CreateAsync(HttpClient, "Tenant A secret", CancellationToken);

        Account intruder = await RegisterAndLoginAsync();
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
    public async Task OtherTenant_Should_NotSeeUsers()
    {
        // Arrange
        Account owner = await RegisterAndLoginAsync();
        Account intruder = await RegisterAndLoginAsync();
        Authenticate(intruder.Tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{owner.UserId}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserList_Should_NotIncludeOtherTenantsUsers()
    {
        // Arrange
        Account owner = await RegisterAndLoginAsync();
        Account intruder = await RegisterAndLoginAsync();
        Authenticate(intruder.Tokens.AccessToken);

        // Act
        UserPageDto? page = await HttpClient.GetFromJsonAsync<UserPageDto>("users?pageSize=100", CancellationToken);

        // Assert
        page!.Items.ShouldNotContain(u => u.Id == owner.UserId);
        page.Items.ShouldAllBe(u => u.Id == intruder.UserId);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task EachRegistration_Should_GetItsOwnTenant()
    {
        // Arrange
        Account first = await RegisterAndLoginAsync();
        Account second = await RegisterAndLoginAsync();

        // Act
        Authenticate(first.Tokens.AccessToken);
        UserDto firstUser = (await HttpClient.GetFromJsonAsync<UserDto>($"users/{first.UserId}", CancellationToken))!;
        Authenticate(second.Tokens.AccessToken);
        UserDto secondUser = (await HttpClient.GetFromJsonAsync<UserDto>($"users/{second.UserId}", CancellationToken))!;

        // Assert
        firstUser.TenantId.ShouldNotBe(Guid.Empty);
        firstUser.TenantId.ShouldNotBe(secondUser.TenantId);
    }
}
