using CleanArchitecture.Application.Todos.Copy;
using CleanArchitecture.Application.Todos.Create;
using CleanArchitecture.Application.Todos.Update;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.UnitTests.Fakes;
using FluentValidation.TestHelper;

namespace CleanArchitecture.UnitTests.Todos;

public sealed class TodoValidatorsTests
{
    private readonly CreateTodoCommandValidator _createValidator = new(TestData.Clock());
    private readonly UpdateTodoCommandValidator _updateValidator = new();
    private readonly CopyTodoCommandValidator _copyValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionIsEmpty() =>
        _createValidator.TestValidate(new CreateTodoCommand(string.Empty, null, [], Priority.Low))
            .ShouldHaveValidationErrorFor(c => c.Description);

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength() =>
        _createValidator.TestValidate(new CreateTodoCommand(
                new string('a', TodoItem.DescriptionMaxLength + 1), null, [], Priority.Low))
            .ShouldHaveValidationErrorFor(c => c.Description);

    [Fact]
    public void CreateValidator_Should_HaveError_WhenPriorityIsNotDefined() =>
        _createValidator.TestValidate(new CreateTodoCommand("Buy groceries", null, [], (Priority)99))
            .ShouldHaveValidationErrorFor(c => c.Priority);

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDueDateIsInThePast() =>
        _createValidator.TestValidate(new CreateTodoCommand("Buy groceries", TestData.UtcNow.AddDays(-1), [], Priority.Low))
            .ShouldHaveValidationErrorFor(c => c.DueDate);

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid() =>
        _createValidator.TestValidate(new CreateTodoCommand("Buy groceries", TestData.UtcNow.Date, ["home"], Priority.Medium))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void UpdateValidator_Should_HaveErrors_WhenIdIsEmptyAndDescriptionTooLong()
    {
        TestValidationResult<UpdateTodoCommand> result = _updateValidator.TestValidate(
            new UpdateTodoCommand(Guid.Empty, new string('a', TodoItem.DescriptionMaxLength + 1)));

        result.ShouldHaveValidationErrorFor(c => c.TodoItemId);
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid() =>
        _updateValidator.TestValidate(new UpdateTodoCommand(Guid.NewGuid(), "Updated"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CopyValidator_Should_HaveError_WhenIdIsEmpty() =>
        _copyValidator.TestValidate(new CopyTodoCommand(Guid.Empty)).ShouldHaveValidationErrorFor(c => c.TodoItemId);
}
