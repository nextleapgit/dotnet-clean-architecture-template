using FluentValidation;

namespace CleanArchitecture.Application.Users.Get;

internal sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, GetUsersQuery.MaxPageSize);
    }
}
