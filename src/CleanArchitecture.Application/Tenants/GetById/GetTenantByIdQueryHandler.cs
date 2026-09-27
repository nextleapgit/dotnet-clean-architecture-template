using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants.GetById;

internal sealed class GetTenantByIdQueryHandler(ITenantStore tenantStore, TenantAccess tenantAccess)
    : IQueryHandler<GetTenantByIdQuery, TenantResponse>
{
    public async Task<Result<TenantResponse>> HandleAsync(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        Result<TenantId> tenantId = await tenantAccess.ResolveAsync(query.TenantId, cancellationToken);

        if (tenantId.IsFailure)
        {
            return Result.Failure<TenantResponse>(tenantId.Error);
        }

        TenantResponse? tenant = await tenantStore.GetResponseAsync(tenantId.Value, cancellationToken);

        return tenant is null
            ? Result.Failure<TenantResponse>(TenantErrors.NotFound(query.TenantId))
            : tenant;
    }
}
