namespace CleanArchitecture.Application.Tenants;

/// <summary>Stable audit action names. Treat them as a persisted contract.</summary>
public static class TenantAuditActions
{
    public const string Created = "tenants.created";
    public const string Deactivated = "tenants.deactivated";
    public const string Activated = "tenants.activated";
}
