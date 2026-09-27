using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Tenants;

internal sealed class TenantStore(ApplicationDbContext dbContext) : ITenantStore
{
    public Task<Tenant?> FindAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<bool> ExistsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);

    public Task<bool> IsActiveAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, cancellationToken);

    public Task<bool> IsPlatformAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.AnyAsync(t => t.Id == tenantId && t.IsPlatform, cancellationToken);

    public Task<TenantResponse?> GetResponseAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        ToResponse(dbContext.Tenants.Where(t => t.Id == tenantId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<TenantResponse>> ListResponsesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int totalCount = await dbContext.Tenants.CountAsync(cancellationToken);
        List<TenantResponse> items = await ToResponse(dbContext.Tenants
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<TenantResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public void Add(Tenant tenant) => dbContext.Tenants.Add(tenant);

    private static IQueryable<TenantResponse> ToResponse(IQueryable<Tenant> tenants) =>
        tenants.Select(t => new TenantResponse
        {
            Id = t.Id.Value,
            Name = t.Name,
            IsActive = t.IsActive,
            CreatedAtUtc = t.CreatedAtUtc
        });
}
