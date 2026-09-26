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

    public static Tenant Create(string name, DateTime createdAtUtc)
    {
        var tenant = new Tenant
        {
            Id = TenantId.New(),
            Name = name,
            CreatedAtUtc = createdAtUtc
        };

        tenant.Raise(new TenantCreatedDomainEvent(tenant.Id));

        return tenant;
    }
}
