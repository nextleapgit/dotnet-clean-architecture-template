using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Todos;

internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Description).HasMaxLength(TodoItem.DescriptionMaxLength);

        builder.Property(t => t.DueDate).HasConversion(d => d != null ? DateTime.SpecifyKind(d.Value, DateTimeKind.Utc) : d, v => v);

        builder.HasIndex(t => new { t.TenantId, t.UserId });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(t => t.TenantId);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
    }
}
