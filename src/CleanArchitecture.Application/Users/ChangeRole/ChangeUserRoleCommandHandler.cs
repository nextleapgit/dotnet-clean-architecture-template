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
        Role previousRole = user.Role;

        Result result = user.ChangeRole(command.Role);

        if (result.IsFailure || previousRole == user.Role)
        {
            return result;
        }

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
