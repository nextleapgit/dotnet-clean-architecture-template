# Test Templates

Stack: xUnit v3 on Microsoft.Testing.Platform, Shouldly, NSubstitute. Every use case gets unit, validator, and integration tests; tenant-owned data also gets isolation tests. Pass `TestContext.Current.CancellationToken` to every call that accepts a token (the analyzer enforces it).

## Handler unit tests — `tests/CleanArchitecture.UnitTests/{Feature}/`

No database: handlers get in-memory fakes from `Fakes/` (`InMemoryTodoItemStore`, `InMemoryUserStore`, `InMemoryRefreshTokenStore`, `FakeUnitOfWork`, `FakeTenantContext`, `RecordingAuditLog`, `RecordingEmailOutbox`, `FakeTokenProvider`) and `TestData` (`NewUser`, `NewTodo`, `Clock()`, `UtcNow`). `TodoHandlerFixture` provides a tenant, a user, and a second tenant.

```csharp
public sealed class ArchiveTodoCommandHandlerTests
{
    private readonly TodoHandlerFixture _fixture = new();

    private ArchiveTodoCommandHandler Handler => new(
        _fixture.UnitOfWork, _fixture.Store, _fixture.TenantContext, TestData.Clock(), Caches.Create());

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherTenant()
    {
        // Arrange
        TodoItem foreign = TestData.NewTodo(_fixture.OtherTenantId, _fixture.UserId);
        _fixture.Store.Add(foreign);

        // Act
        Result result = await Handler.HandleAsync(new ArchiveTodoCommand(foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ArchiveTodoAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        TodoItem todoItem = TestData.NewTodo(_fixture.TenantId, _fixture.UserId);
        _fixture.Store.Add(todoItem);

        // Act
        Result result = await Handler.HandleAsync(new ArchiveTodoCommand(todoItem.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.IsArchived.ShouldBeTrue();
        todoItem.DomainEvents.ShouldContain(new TodoItemArchivedDomainEvent(todoItem.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
```

Cover every guard clause (not found, other tenant, other user, business rule) and the happy path (state, event, save count). Audited commands assert the `RecordingAuditLog` record; email-sending commands assert `RecordingEmailOutbox` (it throws outside a transaction, like the real outbox) and `FakeUnitOfWork.Committed`.

## Validator tests — `{Feature}ValidatorsTests.cs`

One failing test per rule plus one valid command, with `FluentValidation.TestHelper` (`TestValidate`, `ShouldHaveValidationErrorFor`, `ShouldNotHaveAnyValidationErrors`).

## Integration tests — `tests/CleanArchitecture.IntegrationTests/{Feature}/`

Inherit `BaseIntegrationTest(factory)`: real API + PostgreSQL (Testcontainers). `HttpClient` resolves relative URLs under `/api/v1/`. Helpers: `CreateAccountAsync(role, tenantId?)` (a new user — in a new **tenant** unless one is given — created by the bootstrapped admin, then signed in), `CreateTenantAsync()`, `CreateUserAsync(email, role, tenantId?)`, `CreateAdminClientAsync()`, `Authenticate(token)`, `CreateClient()` for a second caller, `WithDbContextAsync(...)` to assert persisted rows (e.g. audit entries).

```csharp
[Fact]
public async Task ArchiveTodo_Should_ReturnNotFound_ForAnotherTenant()
{
    // Arrange
    Account owner = await CreateAccountAsync();
    Authenticate(owner.Tokens.AccessToken);
    Guid todoId = await TodoApi.CreateAsync(HttpClient, "Private", CancellationToken);

    Account intruder = await CreateAccountAsync();
    HttpClient intruderClient = CreateClient();
    Authenticate(intruderClient, intruder.Tokens.AccessToken);

    // Act
    HttpResponseMessage response = await intruderClient.PutAsync($"todos/{todoId}/archive", null, CancellationToken);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
```

Minimum per endpoint: happy path with state verified by a follow-up request, a failure translation (status + `code`), and tenant isolation for tenant-owned data. `ApiConventionTests` already enforce versioning and permissions for every route.

## Run

```
dotnet test --solution CleanArchitecture.slnx
```

Integration tests need Docker. Architecture tests fail the build on layer, persistence, or convention violations — fix the code, never the test.
