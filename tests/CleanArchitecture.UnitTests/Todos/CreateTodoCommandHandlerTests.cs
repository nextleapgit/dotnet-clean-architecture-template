using CleanArchitecture.Application.Todos.Create;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class CreateTodoCommandHandlerTests
{
    private static readonly CreateTodoCommand Command = new("Write unit tests", null, ["work"], Priority.Medium);

    private readonly TodoHandlerFixture _fixture = new();
    private readonly InMemoryUserStore _userStore = new();

    private CreateTodoCommandHandler Handler => new(
        _fixture.UnitOfWork,
        _fixture.Store,
        _fixture.TenantContext,
        _userStore,
        TestData.Clock());

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExistInTenant()
    {
        // Act
        Result<Guid> result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFound(_fixture.UserId));
        _fixture.Store.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_PersistTodoForCurrentTenantAndUser_WhenValid()
    {
        // Arrange
        User user = TestData.NewUser(_fixture.TenantId);
        _userStore.Add(user);
        var handler = new CreateTodoCommandHandler(
            _fixture.UnitOfWork,
            _fixture.Store,
            new FakeTenantContext(_fixture.TenantId, user.Id),
            _userStore,
            TestData.Clock());

        // Act
        Result<Guid> result = await handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TodoItem todoItem = _fixture.Store.Items.ShouldHaveSingleItem();
        todoItem.Id.ShouldBe(result.Value);
        todoItem.TenantId.ShouldBe(_fixture.TenantId);
        todoItem.UserId.ShouldBe(user.Id);
        todoItem.Description.ShouldBe(Command.Description);
        todoItem.Priority.ShouldBe(Command.Priority);
        todoItem.CreatedAt.ShouldBe(TestData.UtcNow);
        todoItem.DomainEvents.ShouldContain(new TodoItemCreatedDomainEvent(todoItem.Id));
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
