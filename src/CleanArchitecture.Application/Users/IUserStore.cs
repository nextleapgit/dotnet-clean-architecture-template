using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users;

public interface IUserStore
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Tenant-agnostic by design: the email is a global sign-in credential. Use it for
    /// authentication only — every listing or management query must be tenant-scoped.
    /// </summary>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken);

    Task<UserResponse?> GetResponseAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken);

    void Add(User user);
}
