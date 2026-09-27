using CleanArchitecture.Application.Users;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants;

/// <summary>
/// Decides which tenant a request may act on. Everyone acts on their own tenant; only an active
/// admin may name another one. Checked here as well as by endpoint permissions, so a use case
/// stays safe even if an endpoint is mapped with the wrong permission.
/// </summary>
internal sealed class TenantAccess(
    ICurrentTenantContext tenantContext,
    IUserStore userStore,
    ITenantStore tenantStore)
{
    public Task<bool> IsAdminAsync(CancellationToken cancellationToken) =>
        userStore.IsActiveAdminAsync(tenantContext.CurrentTenantId, tenantContext.CurrentUserId, cancellationToken);

    /// <param name="requestedTenantId">The tenant named by the request; null means the caller's own.</param>
    public async Task<Result<TenantId>> ResolveAsync(Guid? requestedTenantId, CancellationToken cancellationToken)
    {
        TenantId currentTenantId = tenantContext.CurrentTenantId;

        if (requestedTenantId is null || requestedTenantId.Value == currentTenantId.Value)
        {
            return currentTenantId;
        }

        var requested = new TenantId(requestedTenantId.Value);

        // Non-admins cannot tell another tenant from one that does not exist.
        if (!await IsAdminAsync(cancellationToken) || !await tenantStore.ExistsAsync(requested, cancellationToken))
        {
            return Result.Failure<TenantId>(TenantErrors.NotFound(requestedTenantId.Value));
        }

        return requested;
    }
}
