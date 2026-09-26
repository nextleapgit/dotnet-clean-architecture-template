using CleanArchitecture.Domain.Tenants;

namespace CleanArchitecture.Application.Tenants;

public interface ITenantStore
{
    void Add(Tenant tenant);
}
