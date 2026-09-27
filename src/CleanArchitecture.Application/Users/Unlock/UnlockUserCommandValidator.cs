using FluentValidation;

namespace CleanArchitecture.Application.Users.Unlock;

internal sealed class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
{
    public UnlockUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
