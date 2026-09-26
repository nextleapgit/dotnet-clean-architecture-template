using CleanArchitecture.Application.Todos.Copy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class CopyTodoCommandHandlerTests
{
    private readonly TodoHandlerFixture _fixture = new();

    private CopyTodoCommandHandler Handler => new(
        _fixture.UnitOfWork,
        _fixture.Store,
        _fixture.TenantContext,
        TestData.Clock());

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var command = new CopyTodoCommand(Guid.NewGuid());

        // Act
        Result<Guid> result = await Handler.HandleAsync(command, TestContext.Current.CancellationToken);

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
        Result<Guid> result = await Handler.HandleAsync(new CopyTodoCommand(foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotFound(foreign.Id));
        _fixture.Store.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_PersistUncompletedCopyInSameTenantAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        TodoItem original = TestData.NewTodo(_fixture.TenantId, _fixture.UserId, isCompleted: true);
        _fixture.Store.Add(original);

        // Act
        Result<Guid> result = await Handler.HandleAsync(new CopyTodoCommand(original.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TodoItem copy = _fixture.Store.Items.Single(t => t.Id == result.Value);
        copy.Id.ShouldNotBe(original.Id);
        copy.TenantId.ShouldBe(_fixture.TenantId);
        copy.UserId.ShouldBe(_fixture.UserId);
        copy.Description.ShouldBe(original.Description);
        copy.Labels.ShouldBe(original.Labels);
        copy.IsCompleted.ShouldBeFalse();
        copy.DomainEvents.ShouldContain(new TodoItemCreatedDomainEvent(copy.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
