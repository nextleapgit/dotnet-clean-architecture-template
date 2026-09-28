using FluentValidation;

namespace CleanArchitecture.Application.Users.EmailChange;

internal sealed class ConfirmEmailChangeCommandValidator : AbstractValidator<ConfirmEmailChangeCommand>
{
    public ConfirmEmailChangeCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(128);
    }
}
