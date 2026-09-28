using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CleanArchitecture.IntegrationTests.Todos;

public sealed class TodoHardeningTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Reads_Should_ReflectUpdatesAndDeletes_FromAnotherHost()
    {
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);
        Guid id = await TodoApi.CreateAsync(HttpClient, "Before", CancellationToken);
        (await TodoApi.GetAsync(HttpClient, id, CancellationToken)).Description.ShouldBe("Before");
        await using WebApplicationFactory<Program> otherHost = Factory.WithWebHostBuilder(_ => { });
        using HttpClient other = otherHost.CreateClient();
        Authenticate(other, account.Tokens.AccessToken);
        using HttpResponseMessage update = await other.PutAsJsonAsync($"/api/v1/todos/{id}", new { description = "After" }, CancellationToken);
        update.EnsureSuccessStatusCode();
        (await TodoApi.GetAsync(HttpClient, id, CancellationToken)).Description.ShouldBe("After");
        using HttpResponseMessage delete = await other.DeleteAsync($"/api/v1/todos/{id}", CancellationToken);
        delete.EnsureSuccessStatusCode();
        using HttpResponseMessage missing = await HttpClient.GetAsync($"todos/{id}", CancellationToken);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_Should_ReturnBoundedDisjointPages_AndRejectOverflow()
    {
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);
        for (int index = 0; index < 3; index++)
        {
            await TodoApi.CreateAsync(HttpClient, $"Item {index}", CancellationToken);
        }
        TodosTests.TodoDto[] first = (await HttpClient.GetFromJsonAsync<TodosTests.TodoDto[]>("todos?page=1&pageSize=2", CancellationToken))!;
        TodosTests.TodoDto[] second = (await HttpClient.GetFromJsonAsync<TodosTests.TodoDto[]>("todos?page=2&pageSize=2", CancellationToken))!;
        first.Length.ShouldBe(2);
        second.Length.ShouldBe(1);
        first.Select(t => t.Id).Intersect(second.Select(t => t.Id)).ShouldBeEmpty();
        using HttpResponseMessage large = await HttpClient.GetAsync("todos?page=2147483647&pageSize=100", CancellationToken);
        using HttpResponseMessage oversize = await HttpClient.GetAsync("todos?pageSize=101", CancellationToken);
        large.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        oversize.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
