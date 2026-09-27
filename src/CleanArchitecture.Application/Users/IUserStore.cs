using CleanArchitecture.Application.Abstractions.Paging;
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

    /// <summary>A tracked user of the tenant, for use cases that change it.</summary>
    Task<User?> FindAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken);

    Task<bool> IsActiveAdminAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Tenant-agnostic by design: used only by the start-up bootstrap.</summary>
    Task<bool> AdminExistsAsync(CancellationToken cancellationToken);

    Task<UserResponse?> GetResponseAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Users of one tenant, ordered by email.</summary>
    Task<PagedResponse<UserResponse>> ListResponsesAsync(
        TenantId tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    void Add(User user);
}
