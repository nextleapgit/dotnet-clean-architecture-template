using FluentValidation;

namespace CleanArchitecture.Application.Users.Passwords;

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).ValidPassword().NotEqual(c => c.CurrentPassword);
    }
}
