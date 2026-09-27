using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.HasKey(userToken => userToken.Id);

        builder.Property(userToken => userToken.TokenHash)
            .HasMaxLength(UserToken.TokenHashLength)
            .IsFixedLength();

        builder.Property(userToken => userToken.Payload).HasMaxLength(User.EmailMaxLength);

        builder.HasIndex(userToken => userToken.TokenHash).IsUnique();

        builder.HasIndex(userToken => new { userToken.UserId, userToken.Purpose });

        // Maps to PostgreSQL's xmin: a link used twice at the same moment succeeds only once.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne(userToken => userToken.User)
            .WithMany()
            .HasForeignKey(userToken => userToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
