using CleanArchitecture.Domain.Todos;
using FluentValidation;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos.Create;

internal sealed class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TodoItem.DescriptionMaxLength);
        RuleFor(c => c.Labels).NotNull();
        RuleFor(c => c.DueDate)
            .GreaterThanOrEqualTo(_ => dateTimeProvider.UtcNow.Date)
            .When(c => c.DueDate.HasValue);
    }
}
