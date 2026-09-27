using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.ChangeRole;

internal sealed class ChangeUserRoleCommandHandler(
    IUnitOfWork unitOfWork,
    UserManagement userManagement,
    IAuditLog auditLog)
    : ICommandHandler<ChangeUserRoleCommand>
{
    public async Task<Result> HandleAsync(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        Result<User> found = await userManagement.FindManageableAsync(
            command.TenantId,
            command.UserId,
            allowSelf: false,
            cancellationToken);

        if (found.IsFailure)
        {
            return found;
        }

        User user = found.Value;

        if (user.Role == command.Role)
        {
            return Result.Success();
        }

        Result assignable = await userManagement.EnsureRoleAssignableAsync(command.Role, user.TenantId, cancellationToken);

        if (assignable.IsFailure)
        {
            return assignable;
        }

        Role previousRole = user.Role;

        user.ChangeRole(command.Role);

        auditLog.Record(new AuditRecord(UserAuditActions.RoleChanged, nameof(User), user.Id.ToString(), AuditSeverity.Warning)
        {
            TenantId = user.TenantId,
            OldValues = new { role = previousRole.ToString() },
            NewValues = new { role = user.Role.ToString() }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
