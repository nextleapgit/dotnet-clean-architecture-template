using FluentValidation;

namespace CleanArchitecture.Application.Todos.Get;

internal sealed class GetTodosQueryValidator : AbstractValidator<GetTodosQuery>
{
    public GetTodosQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, 10000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
