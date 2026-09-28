using CleanArchitecture.Application.Todos.Update;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class UpdateTodoCommandHandlerTests
{
    private readonly TodoHandlerFixture _fixture = new();

    private UpdateTodoCommandHandler Handler => new(
        _fixture.UnitOfWork,
        _fixture.Store,
        _fixture.TenantContext);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var command = new UpdateTodoCommand(Guid.NewGuid(), "Updated");

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
        Result result = await Handler.HandleAsync(
            new UpdateTodoCommand(foreign.Id, "Updated"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
        foreign.Description.ShouldBe("Existing todo");
    }

    [Fact]
    public async Task Handle_Should_UpdateDescriptionAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        TodoItem todoItem = TestData.NewTodo(_fixture.TenantId, _fixture.UserId);
        _fixture.Store.Add(todoItem);

        // Act
        Result result = await Handler.HandleAsync(
            new UpdateTodoCommand(todoItem.Id, "Updated"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.Description.ShouldBe("Updated");
        todoItem.DomainEvents.ShouldContain(new TodoItemUpdatedDomainEvent(todoItem.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
