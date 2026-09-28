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
        RuleFor(c => c.Labels).NotNull().Must(labels => labels is null || labels.Count <= TodoItem.MaxLabels)
            .WithMessage("At most 20 labels are allowed.");
        RuleForEach(c => c.Labels).NotEmpty().MaximumLength(TodoItem.LabelMaxLength);
        RuleFor(c => c.DueDate)
            .GreaterThanOrEqualTo(_ => dateTimeProvider.UtcNow.Date)
            .When(c => c.DueDate.HasValue);
    }
}
