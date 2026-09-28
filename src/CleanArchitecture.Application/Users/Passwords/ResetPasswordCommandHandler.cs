using System.Globalization;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Passwords;

internal sealed class ResetPasswordCommandHandler(
    IUnitOfWork unitOfWork,
    UserTokenIssuer userTokenIssuer,
    ITenantStore tenantStore,
    IRefreshTokenStore refreshTokenStore,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        UserToken? reset = await userTokenIssuer.FindUsableAsync(command.Token, UserTokenPurpose.PasswordReset, cancellationToken);

        if (reset is null
            || !reset.User.IsActive
            || !await tenantStore.IsActiveAsync(reset.User.TenantId, cancellationToken))
        {
            return Result.Failure(UserErrors.InvalidOrExpiredToken);
        }

        User user = reset.User;
        DateTime utcNow = dateTimeProvider.UtcNow;

        // Also lifts a lockout: the user proved control of the mailbox.
        user.SetPassword(passwordHasher.Hash(command.NewPassword));
        await userTokenIssuer.InvalidateAsync(user.Id, UserTokenPurpose.PasswordReset, cancellationToken);

        IReadOnlyList<RefreshToken> sessions = await refreshTokenStore.GetActiveForUserAsync(user.Id, utcNow, cancellationToken);

        foreach (RefreshToken session in sessions)
        {
            session.Revoke(utcNow);
        }

        auditLog.Record(new AuditRecord(UserAuditActions.PasswordReset, nameof(User), user.Id.ToString(), AuditSeverity.Warning)
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Metadata = new Dictionary<string, string>
            {
                ["revokedSessions"] = sessions.Count.ToString(CultureInfo.InvariantCulture)
            }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
