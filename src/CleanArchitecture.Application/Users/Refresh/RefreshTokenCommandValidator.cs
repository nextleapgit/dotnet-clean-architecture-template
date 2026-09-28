using FluentValidation;

namespace CleanArchitecture.Application.Users.Refresh;

internal sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(128);
    }
}
