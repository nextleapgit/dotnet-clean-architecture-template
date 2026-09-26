using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class InMemoryUserStore : IUserStore
{
    public List<User> Users { get; } = [];

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Exists(u => u.Email == normalizedEmail));

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Email == normalizedEmail));

    public Task<bool> ExistsAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Exists(u => u.TenantId == tenantId && u.Id == userId));

    public Task<UserResponse?> GetResponseAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                TenantId = u.TenantId.Value,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .SingleOrDefault());

    public void Add(User user) => Users.Add(user);
}
