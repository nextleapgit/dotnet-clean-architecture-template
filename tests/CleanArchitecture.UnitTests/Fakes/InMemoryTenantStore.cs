using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class InMemoryTenantStore : ITenantStore
{
    public List<Tenant> Tenants { get; } = [];

    public Task<Tenant?> FindAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Tenants.SingleOrDefault(t => t.Id == tenantId));

    public Task<bool> ExistsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Tenants.Exists(t => t.Id == tenantId));

    public Task<bool> IsActiveAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Tenants.Exists(t => t.Id == tenantId && t.IsActive));

    public Task<TenantResponse?> GetResponseAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Tenants.Where(t => t.Id == tenantId).Select(ToResponse).SingleOrDefault());

    public Task<PagedResponse<TenantResponse>> ListResponsesAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResponse<TenantResponse>
        {
            Items = Tenants
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToResponse)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = Tenants.Count
        });

    public void Add(Tenant tenant) => Tenants.Add(tenant);

    private static TenantResponse ToResponse(Tenant t) => new()
    {
        Id = t.Id.Value,
        Name = t.Name,
        IsActive = t.IsActive,
        CreatedAtUtc = t.CreatedAtUtc
    };
}
