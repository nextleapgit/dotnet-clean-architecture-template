using FluentValidation;

namespace CleanArchitecture.Application.Todos.Copy;

internal sealed class CopyTodoCommandValidator : AbstractValidator<CopyTodoCommand>
{
    public CopyTodoCommandValidator()
    {
        RuleFor(c => c.TodoItemId).NotEmpty();
    }
}
