using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants;

/// <summary>Tenant-wide reads are for platform administration only; see <see cref="TenantAccess"/>.</summary>
public interface ITenantStore
{
    Task<Tenant?> FindAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>False for an inactive or unknown tenant.</summary>
    Task<bool> IsActiveAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<bool> IsPlatformAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<TenantResponse?> GetResponseAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>All tenants, ordered by name.</summary>
    Task<PagedResponse<TenantResponse>> ListResponsesAsync(int page, int pageSize, CancellationToken cancellationToken);

    void Add(Tenant tenant);
}
