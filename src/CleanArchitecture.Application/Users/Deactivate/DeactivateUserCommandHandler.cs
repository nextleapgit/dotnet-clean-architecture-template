using System.Globalization;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Deactivate;

internal sealed class DeactivateUserCommandHandler(
    IUnitOfWork unitOfWork,
    UserManagement userManagement,
    IRefreshTokenStore refreshTokenStore,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog)
    : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Result> HandleAsync(DeactivateUserCommand command, CancellationToken cancellationToken)
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

        if (!user.IsActive)
        {
            return Result.Success();
        }

        user.Deactivate();

        // Access tokens stop working at once (permissions are checked against the database);
        // revoking the sessions stops the user from obtaining new ones.
        DateTime utcNow = dateTimeProvider.UtcNow;
        IReadOnlyList<RefreshToken> sessions = await refreshTokenStore.GetActiveForUserAsync(user.Id, utcNow, cancellationToken);

        foreach (RefreshToken session in sessions)
        {
            session.Revoke(utcNow);
        }

        auditLog.Record(new AuditRecord(UserAuditActions.Deactivated, nameof(User), user.Id.ToString(), AuditSeverity.Warning)
        {
            TenantId = user.TenantId,
            Metadata = new Dictionary<string, string>
            {
                ["revokedSessions"] = sessions.Count.ToString(CultureInfo.InvariantCulture)
            }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
