using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Tenants;

public sealed record TenantDeactivatedDomainEvent(TenantId TenantId) : IDomainEvent;
