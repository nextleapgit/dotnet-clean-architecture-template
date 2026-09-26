using FluentValidation;

namespace CleanArchitecture.Application.Todos.Complete;

internal sealed class CompleteTodoCommandValidator : AbstractValidator<CompleteTodoCommand>
{
    public CompleteTodoCommandValidator()
    {
        RuleFor(c => c.TodoItemId).NotEmpty();
    }
}
