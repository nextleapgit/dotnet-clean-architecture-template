using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users;

/// <summary>Enforces who may manage whom, and who may hand out which role.</summary>
internal sealed class UserManagement(
    TenantAccess tenantAccess,
    IUserStore userStore,
    ITenantStore tenantStore,
    ICurrentTenantContext tenantContext)
{
    /// <summary>Loads a user the caller may manage.</summary>
    /// <param name="tenantId">The target tenant; null means the caller's own tenant.</param>
    /// <param name="allowSelf">False for changes that could lock the caller out.</param>
    public async Task<Result<User>> FindManageableAsync(
        Guid? tenantId,
        Guid userId,
        bool allowSelf,
        CancellationToken cancellationToken)
    {
        Result<TenantId> tenant = await tenantAccess.ResolveAsync(tenantId, cancellationToken);

        if (tenant.IsFailure)
        {
            return Result.Failure<User>(tenant.Error);
        }

        User? user = await userStore.FindAsync(tenant.Value, userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<User>(UserErrors.NotFound(userId));
        }

        if (!allowSelf && user.Id == tenantContext.CurrentUserId)
        {
            return Result.Failure<User>(UserErrors.CannotChangeOwnAccess);
        }

        if (user.Role == Role.Admin && !await tenantAccess.IsAdminAsync(cancellationToken))
        {
            return Result.Failure<User>(UserErrors.AdminNotManageable);
        }

        return user;
    }

    /// <summary>Admin may be assigned only by an admin, and only within the platform tenant.</summary>
    public async Task<Result> EnsureRoleAssignableAsync(Role role, TenantId tenantId, CancellationToken cancellationToken)
    {
        if (role != Role.Admin)
        {
            return Result.Success();
        }

        return await tenantAccess.IsAdminAsync(cancellationToken) && await tenantStore.IsPlatformAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(UserErrors.AdminRoleNotAssignable);
    }
}
