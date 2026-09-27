using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Tenants;

public sealed class Tenant : Entity
{
    public const int NameMaxLength = 200;

    private Tenant()
    {
    }

    public TenantId Id { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Users of an inactive tenant can neither sign in nor use the API.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The operator's own tenant, home of the admins. Created once, by the start-up bootstrap.</summary>
    public bool IsPlatform { get; private set; }

    public static Tenant Create(string name, DateTime createdAtUtc) => Create(name, createdAtUtc, isPlatform: false);

    public static Tenant CreatePlatform(string name, DateTime createdAtUtc) => Create(name, createdAtUtc, isPlatform: true);

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;

        Raise(new TenantDeactivatedDomainEvent(Id));
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;

        Raise(new TenantActivatedDomainEvent(Id));
    }

    private static Tenant Create(string name, DateTime createdAtUtc, bool isPlatform)
    {
        var tenant = new Tenant
        {
            Id = TenantId.New(),
            Name = name,
            CreatedAtUtc = createdAtUtc,
            IsActive = true,
            IsPlatform = isPlatform
        };

        tenant.Raise(new TenantCreatedDomainEvent(tenant.Id));

        return tenant;
    }
}
