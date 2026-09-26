using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Tenants;

public sealed record TenantCreatedDomainEvent(TenantId TenantId) : IDomainEvent;
