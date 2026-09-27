using CleanArchitecture.Domain.Tenants;
using FluentValidation;

namespace CleanArchitecture.Application.Tenants.Create;

internal sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Tenant.NameMaxLength);
    }
}
