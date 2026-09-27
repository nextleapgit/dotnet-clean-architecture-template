using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Tenants;

public sealed record TenantActivatedDomainEvent(TenantId TenantId) : IDomainEvent;
