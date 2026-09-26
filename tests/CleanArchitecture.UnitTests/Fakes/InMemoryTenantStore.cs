using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Domain.Tenants;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class InMemoryTenantStore : ITenantStore
{
    public List<Tenant> Tenants { get; } = [];

    public void Add(Tenant tenant) => Tenants.Add(tenant);
}
