using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Tenants.Get;

public sealed record GetTenantsQuery(int Page = 1, int PageSize = GetTenantsQuery.DefaultPageSize)
    : IQuery<PagedResponse<TenantResponse>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
