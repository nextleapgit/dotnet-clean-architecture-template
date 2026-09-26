using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Auditing;

/// <summary>
/// What happened, as described by the use case. Request data (correlation id, IP address,
/// user agent, timestamp) and the current tenant/user are added by the audit log itself.
/// </summary>
public sealed record AuditRecord(
    string Action,
    string EntityName,
    string? EntityId,
    AuditSeverity Severity = AuditSeverity.Information)
{
    /// <summary>Overrides the tenant from the request context, e.g. for anonymous sign-up.</summary>
    public TenantId? TenantId { get; init; }

    /// <summary>Overrides the user from the request context, e.g. for login.</summary>
    public Guid? UserId { get; init; }

    public object? OldValues { get; init; }

    public object? NewValues { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
