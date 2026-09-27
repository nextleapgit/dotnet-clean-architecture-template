namespace CleanArchitecture.Application.Tenants;

public sealed record TenantResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
