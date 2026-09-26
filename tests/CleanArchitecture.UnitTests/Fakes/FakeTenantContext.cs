using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class FakeTenantContext(TenantId tenantId, Guid userId) : ICurrentTenantContext
{
    public bool IsAvailable => true;

    public TenantId CurrentTenantId { get; } = tenantId;

    public Guid CurrentUserId { get; } = userId;

    public IReadOnlyCollection<TenantId> AccessibleTenantIds => [CurrentTenantId];
}
