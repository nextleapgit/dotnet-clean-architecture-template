using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants.Get;

internal sealed class GetTenantsQueryHandler(ITenantStore tenantStore, TenantAccess tenantAccess)
    : IQueryHandler<GetTenantsQuery, PagedResponse<TenantResponse>>
{
    public async Task<Result<PagedResponse<TenantResponse>>> HandleAsync(
        GetTenantsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await tenantAccess.IsAdminAsync(cancellationToken))
        {
            return Result.Failure<PagedResponse<TenantResponse>>(UserErrors.Forbidden);
        }

        return await tenantStore.ListResponsesAsync(query.Page, query.PageSize, cancellationToken);
    }
}
