using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users;

/// <summary>Loads the user a manager or admin is about to change, enforcing who may be managed.</summary>
internal sealed class UserManagement(
    TenantAccess tenantAccess,
    IUserStore userStore,
    ICurrentTenantContext tenantContext)
{
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

        if (user.Role == Role.Admin)
        {
            return Result.Failure<User>(UserErrors.AdminNotManageable);
        }

        if (!allowSelf && user.Id == tenantContext.CurrentUserId)
        {
            return Result.Failure<User>(UserErrors.CannotChangeOwnAccess);
        }

        return user;
    }
}
