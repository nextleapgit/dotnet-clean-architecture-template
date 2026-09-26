using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Infrastructure.Database;

namespace CleanArchitecture.Infrastructure.Tenants;

internal sealed class TenantStore(ApplicationDbContext dbContext) : ITenantStore
{
    public void Add(Tenant tenant) => dbContext.Tenants.Add(tenant);
}
