using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Auditing;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(AuditEntry.ActionMaxLength);
        builder.Property(a => a.EntityName).HasMaxLength(AuditEntry.EntityNameMaxLength);
        builder.Property(a => a.EntityId).HasMaxLength(AuditEntry.EntityIdMaxLength);
        builder.Property(a => a.CorrelationId).HasMaxLength(AuditEntry.CorrelationIdMaxLength);
        builder.Property(a => a.IpAddress).HasMaxLength(AuditEntry.IpAddressMaxLength);
        builder.Property(a => a.UserAgent).HasMaxLength(AuditEntry.UserAgentMaxLength);

        builder.Property(a => a.OldValues).HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnType("jsonb");
        builder.Property(a => a.Metadata).HasColumnType("jsonb");

        // No foreign keys: audit evidence must outlive the tenants and users it describes.
        builder.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });
        builder.HasIndex(a => new { a.UserId, a.CreatedAtUtc });
        builder.HasIndex(a => a.Action);
    }
}
