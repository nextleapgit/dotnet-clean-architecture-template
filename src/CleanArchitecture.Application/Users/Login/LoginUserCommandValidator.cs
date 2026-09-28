using FluentValidation;

namespace CleanArchitecture.Application.Users.Login;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Password).NotEmpty().MaximumLength(UserValidationRules.PasswordMaxLength);
    }
}
