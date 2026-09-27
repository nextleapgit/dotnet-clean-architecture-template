using FluentValidation;

namespace CleanArchitecture.Application.Users.Invitations;

internal sealed class ResendInvitationCommandValidator : AbstractValidator<ResendInvitationCommand>
{
    public ResendInvitationCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
