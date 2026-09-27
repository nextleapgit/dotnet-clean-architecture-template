using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class FakeTenantContext(TenantId tenantId, Guid userId, Guid? sessionId = null) : ICurrentTenantContext
{
    public bool IsAvailable => true;

    public TenantId CurrentTenantId { get; } = tenantId;

    public Guid CurrentUserId { get; } = userId;

    public Guid? CurrentSessionId { get; } = sessionId;

    public IReadOnlyCollection<TenantId> AccessibleTenantIds => [CurrentTenantId];
}
