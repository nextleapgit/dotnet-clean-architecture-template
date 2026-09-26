using CleanArchitecture.Application.Todos.Delete;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class DeleteTodoCommandHandlerTests
{
    private readonly TodoHandlerFixture _fixture = new();

    private DeleteTodoCommandHandler Handler => new(
        _fixture.UnitOfWork,
        _fixture.Store,
        _fixture.TenantContext,
        Caches.Create());

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var command = new DeleteTodoCommand(Guid.NewGuid());

        // Act
        Result result = await Handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(command.TodoItemId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherTenant()
    {
        // Arrange
        TodoItem foreign = TestData.NewTodo(_fixture.OtherTenantId, _fixture.UserId);
        _fixture.Store.Add(foreign);

        // Act
        Result result = await Handler.HandleAsync(new DeleteTodoCommand(foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
        _fixture.Store.Items.ShouldContain(foreign);
    }

    [Fact]
    public async Task Handle_Should_RemoveTodoAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        TodoItem todoItem = TestData.NewTodo(_fixture.TenantId, _fixture.UserId);
        _fixture.Store.Add(todoItem);

        // Act
        Result result = await Handler.HandleAsync(new DeleteTodoCommand(todoItem.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _fixture.Store.Items.ShouldBeEmpty();
        todoItem.DomainEvents.ShouldContain(new TodoItemDeletedDomainEvent(todoItem.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
