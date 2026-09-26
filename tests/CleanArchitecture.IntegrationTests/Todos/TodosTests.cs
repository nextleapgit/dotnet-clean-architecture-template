using System.Net;
using System.Net.Http.Json;

namespace CleanArchitecture.IntegrationTests.Todos;

public sealed class TodosTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    internal sealed record TodoDto(Guid Id, Guid UserId, string Description, int Priority, bool IsCompleted);

    [Fact]
    public async Task GetTodo_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos/{Guid.NewGuid()}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTodo_Should_PersistTodoForCurrentUser_ThatCanBeRetrievedById()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        Guid todoId = await TodoApi.CreateAsync(HttpClient, "Integration test todo", CancellationToken);

        // Assert
        TodoDto todo = await TodoApi.GetAsync(HttpClient, todoId, CancellationToken);
        todo.UserId.ShouldBe(account.UserId);
        todo.Description.ShouldBe("Integration test todo");
        todo.Priority.ShouldBe(2);
        todo.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnValidationProblem_WhenRequestIsInvalid()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "todos",
            new { description = string.Empty, priority = 99 },
            CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemBody problem = (await response.Content.ReadFromJsonAsync<ProblemBody>(CancellationToken))!;
        problem.Code.ShouldBe("Validation.General");
        problem.Errors!.Length.ShouldBe(2);
    }

    [Fact]
    public async Task CompleteCopyAndDelete_Should_WorkForOwnTodo()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);
        Guid todoId = await TodoApi.CreateAsync(HttpClient, "Lifecycle", CancellationToken);

        // Act
        HttpResponseMessage complete = await HttpClient.PutAsync($"todos/{todoId}/complete", null, CancellationToken);
        HttpResponseMessage completeAgain = await HttpClient.PutAsync($"todos/{todoId}/complete", null, CancellationToken);
        HttpResponseMessage copy = await HttpClient.PostAsync($"todos/{todoId}/copy", null, CancellationToken);
        Guid copyId = await copy.Content.ReadFromJsonAsync<Guid>(CancellationToken);
        HttpResponseMessage delete = await HttpClient.DeleteAsync($"todos/{todoId}", CancellationToken);

        // Assert
        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        completeAgain.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await TodoApi.GetAsync(HttpClient, copyId, CancellationToken)).IsCompleted.ShouldBeFalse();
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.GetAsync($"todos/{todoId}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTodo_Should_InvalidateCachedTodo()
    {
        // Arrange
        Account account = await RegisterAndLoginAsync();
        Authenticate(account.Tokens.AccessToken);
        Guid todoId = await TodoApi.CreateAsync(HttpClient, "Before", CancellationToken);
        await TodoApi.GetAsync(HttpClient, todoId, CancellationToken); // warm the cache

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"todos/{todoId}",
            new { description = "After" },
            CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TodoApi.GetAsync(HttpClient, todoId, CancellationToken)).Description.ShouldBe("After");
    }
}

internal static class TodoApi
{
    private static readonly string[] Labels = ["integration"];

    public static async Task<Guid> CreateAsync(HttpClient client, string description, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "todos",
            new { description, labels = Labels, priority = 2 },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public static async Task<TodosTests.TodoDto> GetAsync(HttpClient client, Guid todoId, CancellationToken cancellationToken) =>
        (await client.GetFromJsonAsync<TodosTests.TodoDto>($"todos/{todoId}", cancellationToken))!;
}

internal sealed record ProblemBody(string Code, string? CorrelationId, string? Detail, object[]? Errors);
