using CleanArchitecture.Application.Todos;
using CleanArchitecture.Application.Todos.Get;
using CleanArchitecture.Application.Todos.GetById;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class TodoQueryHandlerTests
{
    private readonly TodoHandlerFixture _fixture = new();

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenTodoBelongsToAnotherTenant()
    {
        // Arrange
        TodoItem foreign = TestData.NewTodo(_fixture.OtherTenantId, _fixture.UserId);
        _fixture.Store.Add(foreign);
        var handler = new GetTodoByIdQueryHandler(_fixture.Store, _fixture.TenantContext);

        // Act
        Result<TodoResponse> result = await handler.HandleAsync(
            new GetTodoByIdQuery(foreign.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
    }

    [Fact]
    public async Task GetById_Should_ReturnTodo_WhenOwnedByCurrentUser()
    {
        // Arrange
        TodoItem todoItem = TestData.NewTodo(_fixture.TenantId, _fixture.UserId);
        _fixture.Store.Add(todoItem);
        var handler = new GetTodoByIdQueryHandler(_fixture.Store, _fixture.TenantContext);

        // Act
        Result<TodoResponse> result = await handler.HandleAsync(
            new GetTodoByIdQuery(todoItem.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.Id.ShouldBe(todoItem.Id);
        result.Value.Priority.ShouldBe(todoItem.Priority);
    }

    [Fact]
    public async Task Get_Should_ReturnOnlyTodosOfCurrentTenantAndUser()
    {
        // Arrange
        TodoItem own = TestData.NewTodo(_fixture.TenantId, _fixture.UserId);
        _fixture.Store.Add(own);
        _fixture.Store.Add(TestData.NewTodo(_fixture.OtherTenantId, _fixture.UserId));
        _fixture.Store.Add(TestData.NewTodo(_fixture.TenantId, Guid.NewGuid()));
        var handler = new GetTodosQueryHandler(_fixture.Store, _fixture.TenantContext);

        // Act
        Result<List<TodoResponse>> result = await handler.HandleAsync(
            new GetTodosQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.ShouldHaveSingleItem().Id.ShouldBe(own.Id);
    }
}
