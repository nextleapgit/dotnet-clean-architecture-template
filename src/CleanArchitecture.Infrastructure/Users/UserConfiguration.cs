using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.LastName).HasMaxLength(User.NameMaxLength);

        // Emails are a global sign-in credential, unique across tenants.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId);
    }
}
