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
        UserTokens = new InMemoryUserTokenStore(Users);
        EmailOutbox = new RecordingEmailOutbox(UnitOfWork);

        Platform = AddTenant("Platform", isPlatform: true);
        Tenant = AddTenant("Acme");
        OtherTenant = AddTenant("Globex");
        Admin = AddUser(Platform, Role.Admin);
        Manager = AddUser(Tenant, Role.Manager);
    }

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public InMemoryUserStore Users { get; } = new();

    public InMemoryTenantStore Tenants { get; } = new();

    public InMemoryRefreshTokenStore RefreshTokens { get; } = new();

    public InMemoryUserTokenStore UserTokens { get; }

    public RecordingAuditLog AuditLog { get; } = new();

    public RecordingEmailOutbox EmailOutbox { get; }

    public FakeTokenProvider TokenProvider { get; } = new();

    public FakeClientLinks ClientLinks { get; } = new();

    public Tenant Platform { get; }

    public Tenant Tenant { get; }

    public Tenant OtherTenant { get; }

    public User Admin { get; }

    public User Manager { get; }

    public Tenant AddTenant(string name, bool isPlatform = false)
    {
        Tenant tenant = TestData.NewTenant(name, isPlatform);
        Tenants.Add(tenant);

        return tenant;
    }

    public User AddUser(Tenant tenant, Role role = Role.Member)
    {
        User user = TestData.NewUser(tenant.Id, $"user{++_emailCounter}@example.com", role);
        Users.Add(user);

        return user;
    }

    public FakeTenantContext ContextOf(User actor, Guid? sessionId = null) => new(actor.TenantId, actor.Id, sessionId);

    internal TenantAccess TenantAccessFor(User actor) => new(ContextOf(actor), Users, Tenants);

    internal UserManagement UserManagementFor(User actor) =>
        new(TenantAccessFor(actor), Users, Tenants, ContextOf(actor));

    internal UserTokenIssuer TokenIssuer() => new(UserTokens, TokenProvider, TestData.Clock());

    /// <summary>Issues a token as the application would and returns the raw value that would be emailed.</summary>
    internal string IssueToken(User user, UserTokenPurpose purpose, TimeSpan? lifetime = null, string? payload = null)
    {
        string raw = $"raw-{Guid.NewGuid():N}";
        UserTokens.Add(UserToken.Issue(
            user.Id, purpose, TokenProvider.HashOpaqueToken(raw), TestData.UtcNow, lifetime ?? TimeSpan.FromHours(1), payload));

        return raw;
    }
}
