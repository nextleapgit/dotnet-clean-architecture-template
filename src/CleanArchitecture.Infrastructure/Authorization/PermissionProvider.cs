using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Authorization;

internal sealed class PermissionProvider(ApplicationDbContext dbContext)
{
    /// <summary>
    /// Read from the database on every request, not from the token, so that deactivating a user
    /// or tenant and changing a role take effect immediately. An inactive user or tenant has none.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Role? role = await (
                from user in dbContext.Users.AsNoTracking()
                join tenant in dbContext.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
                where user.Id == userId && user.IsActive && tenant.IsActive
                select (Role?)user.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role is null ? new HashSet<string>() : RolePermissions.For(role.Value);
    }
}
