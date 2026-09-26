using CleanArchitecture.BuildingBlocks.Auditing;

namespace CleanArchitecture.Infrastructure.Auditing;

/// <summary>A persisted audit entry. Append-only: rows are never updated or deleted.</summary>
internal sealed class AuditEntry
{
    public const int ActionMaxLength = 100;
    public const int EntityNameMaxLength = 100;
    public const int EntityIdMaxLength = 100;
    public const int CorrelationIdMaxLength = 64;
    public const int IpAddressMaxLength = 45;
    public const int UserAgentMaxLength = 512;

    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? UserId { get; init; }
    public string CorrelationId { get; init; }
    public string Action { get; init; }
    public string EntityName { get; init; }
    public string? EntityId { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? Metadata { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public AuditSeverity Severity { get; init; }
}
