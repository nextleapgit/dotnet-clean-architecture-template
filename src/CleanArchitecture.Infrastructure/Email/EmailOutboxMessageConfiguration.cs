using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Email;

internal sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public const string TableName = "email_outbox_messages";

    private const string Now = "date_trunc('milliseconds', now())";

    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("ck_email_outbox_messages_status", "status IN (0, 1, 2, 3, 4)");
            table.HasCheckConstraint("ck_email_outbox_messages_attempt_count", "attempt_count >= 0");
            table.HasCheckConstraint(
                "ck_email_outbox_messages_payload",
                "(status IN (0, 1)) = (payload IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_email_outbox_messages_processed_at",
                "(status IN (2, 3, 4)) = (processed_at_utc IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_email_outbox_messages_lease",
                "(status = 1) = (lease_id IS NOT NULL AND lease_expires_at_utc IS NOT NULL)");
        });

        builder.HasKey(m => m.Id);

        // The id is the caller's logical id; a duplicate must fail on the primary key.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.CreatedAtUtc).HasDefaultValueSql(Now).ValueGeneratedOnAdd();
        builder.Property(m => m.NextAttemptAtUtc).HasDefaultValueSql(Now).ValueGeneratedOnAdd();
        builder.Property(m => m.ErrorCode).HasMaxLength(EmailOutboxMessage.ErrorCodeMaxLength);

        builder.HasIndex(m => m.NextAttemptAtUtc)
            .HasDatabaseName("ix_email_outbox_messages_due_pending")
            .HasFilter("status = 0");

        builder.HasIndex(m => m.LeaseExpiresAtUtc)
            .HasDatabaseName("ix_email_outbox_messages_expired_lease")
            .HasFilter("status = 1");

        builder.HasIndex(m => m.ExpiresAtUtc)
            .HasDatabaseName("ix_email_outbox_messages_expiry")
            .HasFilter("status IN (0, 1)");

        builder.HasIndex(m => m.ProcessedAtUtc)
            .HasDatabaseName("ix_email_outbox_messages_terminal_cleanup")
            .HasFilter("status IN (2, 3, 4)");
    }
}
