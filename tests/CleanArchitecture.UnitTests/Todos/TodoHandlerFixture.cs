using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Todos;

/// <summary>A signed-in user in a tenant, plus a user of another tenant for isolation tests.</summary>
public sealed class TodoHandlerFixture
{
    public TenantId TenantId { get; } = TenantId.New();

    public Guid UserId { get; } = Guid.NewGuid();

    public TenantId OtherTenantId { get; } = TenantId.New();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public InMemoryTodoItemStore Store { get; } = new();

    public FakeTenantContext TenantContext => new(TenantId, UserId);
}
