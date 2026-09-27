using FluentValidation;

namespace CleanArchitecture.Application.Users.Deactivate;

internal sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
