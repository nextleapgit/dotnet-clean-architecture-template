using CleanArchitecture.Application.Abstractions.Paging;
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
        dbContext.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task LockForUpdateAsync(User user, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM " + Schemas.Default + ".users WHERE id = {0} FOR UPDATE",
            [user.Id],
            cancellationToken);

        await dbContext.Entry(user).ReloadAsync(cancellationToken);
    }

    public Task<User?> FindAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        InTenant(tenantId).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<bool> ExistsAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        InTenant(tenantId).AnyAsync(u => u.Id == userId, cancellationToken);

    public Task<bool> IsActiveAdminAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        InTenant(tenantId).AnyAsync(u => u.Id == userId && u.Role == Role.Admin && u.IsActive, cancellationToken);

    public Task<bool> AdminExistsAsync(CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.Role == Role.Admin, cancellationToken);

    public Task<UserResponse?> GetResponseAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        ToResponse(InTenant(tenantId).Where(u => u.Id == userId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<UserResponse>> ListResponsesAsync(
        TenantId tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<User> users = InTenant(tenantId);

        int totalCount = await users.CountAsync(cancellationToken);
        List<UserResponse> items = await ToResponse(users
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public void Add(User user) => dbContext.Users.Add(user);

    // The single place that scopes users for listing and management reads.
    private IQueryable<User> InTenant(TenantId tenantId) =>
        dbContext.Users.Where(u => u.TenantId == tenantId);

    private static IQueryable<UserResponse> ToResponse(IQueryable<User> users) =>
        users.Select(u => new UserResponse
        {
            Id = u.Id,
            TenantId = u.TenantId.Value,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Role = u.Role,
            IsActive = u.IsActive,
            InvitationPending = u.PasswordHash == null,
            LockoutEndUtc = u.LockoutEndUtc
        });
}
