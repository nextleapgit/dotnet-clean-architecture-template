using FluentValidation;

namespace CleanArchitecture.Application.Users.Get;

internal sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, 10000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, GetUsersQuery.MaxPageSize);
    }
}
