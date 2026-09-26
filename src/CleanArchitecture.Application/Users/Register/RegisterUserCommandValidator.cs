using CleanArchitecture.Domain.Users;
using FluentValidation;

namespace CleanArchitecture.Application.Users.Register;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(User.NameMaxLength);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(User.NameMaxLength);
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();
        RuleFor(c => c.Password).NotEmpty().MinimumLength(8);
    }
}
