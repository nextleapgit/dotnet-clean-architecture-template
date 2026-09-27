using FluentValidation;

namespace CleanArchitecture.Application.Users.Update;

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
    }
}
