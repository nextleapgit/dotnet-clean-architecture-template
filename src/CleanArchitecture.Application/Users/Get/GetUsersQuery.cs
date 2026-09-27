using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Get;

/// <param name="TenantId">Another tenant to list (admins only); null lists the caller's own tenant.</param>
public sealed record GetUsersQuery(int Page = 1, int PageSize = GetUsersQuery.DefaultPageSize, Guid? TenantId = null)
    : IQuery<PagedResponse<UserResponse>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
