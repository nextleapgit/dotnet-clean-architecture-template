using FluentValidation;

namespace CleanArchitecture.Application.Users.Passwords;

internal sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
    }
}
