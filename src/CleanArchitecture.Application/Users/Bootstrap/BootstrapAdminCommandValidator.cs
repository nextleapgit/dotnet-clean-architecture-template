using CleanArchitecture.Domain.Tenants;
using FluentValidation;

namespace CleanArchitecture.Application.Users.Bootstrap;

internal sealed class BootstrapAdminCommandValidator : AbstractValidator<BootstrapAdminCommand>
{
    public BootstrapAdminCommandValidator()
    {
        RuleFor(c => c.TenantName).NotEmpty().MaximumLength(Tenant.NameMaxLength);
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
        RuleFor(c => c.Password).ValidPassword();
    }
}
