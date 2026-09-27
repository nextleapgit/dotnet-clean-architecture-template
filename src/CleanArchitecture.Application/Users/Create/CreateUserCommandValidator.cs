using FluentValidation;

namespace CleanArchitecture.Application.Users.Create;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
        RuleFor(c => c.Role).KnownRole();
    }
}
