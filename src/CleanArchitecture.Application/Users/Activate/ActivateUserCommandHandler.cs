using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Activate;

internal sealed class ActivateUserCommandHandler(
    IUnitOfWork unitOfWork,
    UserManagement userManagement,
    IAuditLog auditLog)
    : ICommandHandler<ActivateUserCommand>
{
    public async Task<Result> HandleAsync(ActivateUserCommand command, CancellationToken cancellationToken)
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

        if (user.IsActive)
        {
            return Result.Success();
        }

        user.Activate();

        auditLog.Record(new AuditRecord(UserAuditActions.Activated, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
