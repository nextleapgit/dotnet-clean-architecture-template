using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Get;

public sealed record GetUsersQuery(int Page = 1, int PageSize = GetUsersQuery.DefaultPageSize)
    : IQuery<PagedResponse<UserResponse>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
