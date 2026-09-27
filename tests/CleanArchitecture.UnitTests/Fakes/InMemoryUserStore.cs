using CleanArchitecture.Application.Abstractions.Paging;
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
            .Select(ToResponse)
            .SingleOrDefault());

    public Task<PagedResponse<UserResponse>> ListResponsesAsync(
        TenantId tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var users = Users.Where(u => u.TenantId == tenantId).ToList();

        return Task.FromResult(new PagedResponse<UserResponse>
        {
            Items = users
                .OrderBy(u => u.Email, StringComparer.Ordinal)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToResponse)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = users.Count
        });
    }

    public void Add(User user) => Users.Add(user);

    private static UserResponse ToResponse(User u) => new()
    {
        Id = u.Id,
        TenantId = u.TenantId.Value,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName
    };
}
