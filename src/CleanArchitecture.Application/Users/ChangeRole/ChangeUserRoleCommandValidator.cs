using FluentValidation;

namespace CleanArchitecture.Application.Users.ChangeRole;

internal sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Role).AssignableRole();
    }
}
