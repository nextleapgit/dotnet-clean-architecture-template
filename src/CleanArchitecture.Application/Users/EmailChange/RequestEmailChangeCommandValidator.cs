using FluentValidation;

namespace CleanArchitecture.Application.Users.EmailChange;

internal sealed class RequestEmailChangeCommandValidator : AbstractValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty().MaximumLength(UserValidationRules.PasswordMaxLength);
        RuleFor(c => c.NewEmail).ValidEmail();
    }
}
