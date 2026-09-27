using FluentValidation;

namespace CleanArchitecture.Application.Users.Activate;

internal sealed class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
