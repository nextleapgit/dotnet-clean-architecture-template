using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

/// <summary>
/// A platform tenant with an admin, and a customer tenant with a manager — plus a second
/// customer tenant — so tests can act as either role, inside or across tenants.
/// </summary>
public sealed class UserAdministrationFixture
{
    private int _emailCounter;

    public UserAdministrationFixture()
    {
        Platform = AddTenant("Platform");
        Tenant = AddTenant("Acme");
        OtherTenant = AddTenant("Globex");
        Admin = AddUser(Platform, Role.Admin);
        Manager = AddUser(Tenant, Role.Manager);
    }

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public InMemoryUserStore Users { get; } = new();

    public InMemoryTenantStore Tenants { get; } = new();

    public InMemoryRefreshTokenStore RefreshTokens { get; } = new();

    public RecordingAuditLog AuditLog { get; } = new();

    public Tenant Platform { get; }

    public Tenant Tenant { get; }

    public Tenant OtherTenant { get; }

    public User Admin { get; }

    public User Manager { get; }

    public Tenant AddTenant(string name)
    {
        Tenant tenant = TestData.NewTenant(name);
        Tenants.Add(tenant);

        return tenant;
    }

    public User AddUser(Tenant tenant, Role role = Role.Member)
    {
        User user = TestData.NewUser(tenant.Id, $"user{++_emailCounter}@example.com", role);
        Users.Add(user);

        return user;
    }

    public FakeTenantContext ContextOf(User actor) => new(actor.TenantId, actor.Id);

    internal TenantAccess TenantAccessFor(User actor) => new(ContextOf(actor), Users, Tenants);

    internal UserManagement UserManagementFor(User actor) => new(TenantAccessFor(actor), Users, ContextOf(actor));
}
