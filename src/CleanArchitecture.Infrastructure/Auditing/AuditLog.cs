using System.Diagnostics;
using System.Text.Json;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace CleanArchitecture.Infrastructure.Auditing;

/// <summary>
/// Adds audit entries to the current unit of work; they are persisted by the use case's own save,
/// so an audit entry never exists for a change that was rolled back.
/// </summary>
internal sealed class AuditLog(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider) : IAuditLog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Record(AuditRecord record)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        dbContext.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = record.TenantId?.Value ?? (tenantContext.IsAvailable ? tenantContext.CurrentTenantId.Value : null),
            UserId = record.UserId ?? (tenantContext.IsAvailable ? tenantContext.CurrentUserId : null),
            CorrelationId = Truncate(
                httpContext?.TraceIdentifier ?? Activity.Current?.TraceId.ToString() ?? "none",
                AuditEntry.CorrelationIdMaxLength) ?? "none",
            Action = record.Action,
            EntityName = record.EntityName,
            EntityId = record.EntityId,
            OldValues = Serialize(record.OldValues),
            NewValues = Serialize(record.NewValues),
            Metadata = Serialize(record.Metadata),
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(httpContext?.Request.Headers.UserAgent.ToString(), AuditEntry.UserAgentMaxLength),
            CreatedAtUtc = dateTimeProvider.UtcNow,
            Severity = record.Severity
        });
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
