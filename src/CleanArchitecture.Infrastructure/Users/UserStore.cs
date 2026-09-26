using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class UserStore(ApplicationDbContext dbContext) : IUserStore
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> ExistsAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);

    public Task<UserResponse?> GetResponseAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                TenantId = u.TenantId.Value,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}
