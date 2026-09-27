using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Unlock;

internal sealed class UnlockUserCommandHandler(
    IUnitOfWork unitOfWork,
    UserManagement userManagement,
    IAuditLog auditLog)
    : ICommandHandler<UnlockUserCommand>
{
    public async Task<Result> HandleAsync(UnlockUserCommand command, CancellationToken cancellationToken)
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

        if (user.LockoutEndUtc is null && user.FailedLoginAttempts == 0)
        {
            return Result.Success();
        }

        user.Unlock();

        auditLog.Record(new AuditRecord(UserAuditActions.Unlocked, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
