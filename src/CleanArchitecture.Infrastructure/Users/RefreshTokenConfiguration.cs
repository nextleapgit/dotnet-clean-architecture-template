using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(refreshToken => refreshToken.Id);

        builder.Property(refreshToken => refreshToken.TokenHash)
            .HasMaxLength(RefreshToken.TokenHashLength)
            .IsFixedLength();

        builder.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();

        builder.HasIndex(refreshToken => refreshToken.FamilyId);

        // Maps to PostgreSQL's xmin: two concurrent rotations of one token cannot both succeed.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne(refreshToken => refreshToken.User)
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.UserId);
    }
}
