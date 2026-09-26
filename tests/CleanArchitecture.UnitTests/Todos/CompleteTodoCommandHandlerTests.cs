using CleanArchitecture.Application.Todos.Complete;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class CompleteTodoCommandHandlerTests
{
    private readonly TodoHandlerFixture _fixture = new();

    private CompleteTodoCommandHandler Handler => new(
        _fixture.UnitOfWork,
        _fixture.Store,
        _fixture.TenantContext,
        TestData.Clock(),
        Caches.Create());

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var command = new CompleteTodoCommand(Guid.NewGuid());

        // Act
        Result result = await Handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(command.TodoItemId));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherTenant()
    {
        // Arrange
        TodoItem foreign = TestData.NewTodo(_fixture.OtherTenantId, _fixture.UserId);
        _fixture.Store.Add(foreign);

        // Act
        Result result = await Handler.HandleAsync(new CompleteTodoCommand(foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
        foreign.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        TodoItem foreign = TestData.NewTodo(_fixture.TenantId, Guid.NewGuid());
        _fixture.Store.Add(foreign);

        // Act
        Result result = await Handler.HandleAsync(new CompleteTodoCommand(foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyCompleted_WhenTodoIsCompleted()
    {
        // Arrange
        TodoItem todoItem = TestData.NewTodo(_fixture.TenantId, _fixture.UserId, isCompleted: true);
        _fixture.Store.Add(todoItem);

        // Act
        Result result = await Handler.HandleAsync(new CompleteTodoCommand(todoItem.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.AlreadyCompleted(todoItem.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_CompleteTodoAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        TodoItem todoItem = TestData.NewTodo(_fixture.TenantId, _fixture.UserId);
        _fixture.Store.Add(todoItem);

        // Act
        Result result = await Handler.HandleAsync(new CompleteTodoCommand(todoItem.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.CompletedAt.ShouldBe(TestData.UtcNow);
        todoItem.DomainEvents.ShouldContain(new TodoItemCompletedDomainEvent(todoItem.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
