using FluentValidation;

namespace CleanArchitecture.Application.Tenants.Get;

internal sealed class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, GetTenantsQuery.MaxPageSize);
    }
}
