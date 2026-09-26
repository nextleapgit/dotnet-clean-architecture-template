using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Domain;

public sealed class EntityTests
{
    [Fact]
    public void TenantCreate_Should_RaiseCreatedEvent()
    {
        var tenant = Tenant.Create("Acme", TestData.UtcNow);

        tenant.DomainEvents.ShouldContain(new TenantCreatedDomainEvent(tenant.Id));
    }

    [Theory]
    [InlineData(" User@Example.COM ", "user@example.com")]
    [InlineData("user@example.com", "user@example.com")]
    public void UserNormalizeEmail_Should_TrimAndLowerCase(string input, string expected) =>
        User.NormalizeEmail(input).ShouldBe(expected);

    [Fact]
    public void TodoComplete_Should_FailTheSecondTime()
    {
        TodoItem todoItem = TestData.NewTodo(TenantId.New(), Guid.NewGuid());

        todoItem.Complete(TestData.UtcNow).IsSuccess.ShouldBeTrue();
        Result second = todoItem.Complete(TestData.UtcNow);

        second.Error.ShouldBe(TodoItemErrors.AlreadyCompleted(todoItem.Id));
    }

    [Fact]
    public void TodoCopy_Should_NotShareTheLabelsList()
    {
        TodoItem original = TestData.NewTodo(TenantId.New(), Guid.NewGuid());

        TodoItem copy = original.Copy(TestData.UtcNow);
        copy.Labels.Add("extra");

        original.Labels.ShouldNotContain("extra");
    }
}
