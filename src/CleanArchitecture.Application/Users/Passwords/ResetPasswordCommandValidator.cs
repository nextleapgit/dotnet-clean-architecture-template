using FluentValidation;

namespace CleanArchitecture.Application.Users.Passwords;

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(128);
        RuleFor(c => c.NewPassword).ValidPassword();
    }
}
