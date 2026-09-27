using FluentValidation;

namespace CleanArchitecture.Application.Users.Invitations;

internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
        RuleFor(c => c.Password).ValidPassword();
    }
}
