using System.Globalization;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Passwords;

internal sealed class ChangePasswordCommandHandler(
    IUnitOfWork unitOfWork,
    IUserStore userStore,
    IRefreshTokenStore refreshTokenStore,
    IPasswordHasher passwordHasher,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox,
    UserTokenIssuer userTokenIssuer)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await userStore.FindAsync(tenantContext.CurrentTenantId, tenantContext.CurrentUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(tenantContext.CurrentUserId));
        }

        // As for sign-in: concurrent attempts run one after another, so every failure is counted.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await userStore.LockForUpdateAsync(user, cancellationToken);

        DateTime utcNow = dateTimeProvider.UtcNow;

        if (user.IsLockedOut(utcNow))
        {
            return Result.Failure(UserErrors.LockedOut);
        }

        // A stolen access token must not become a way to guess the password: failures count towards lockout.
        if (user.PasswordHash is null || !passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            if (user.RecordFailedLogin(utcNow, LockoutPolicy.MaxFailedAttempts, LockoutPolicy.Duration))
            {
                auditLog.Record(new AuditRecord(UserAuditActions.AccountLocked, nameof(User), user.Id.ToString(), AuditSeverity.Warning));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Failure(UserErrors.InvalidCurrentPassword);
        }

        Result<EmailMessage> notice = UserEmails.PasswordChanged(user);

        if (notice.IsFailure)
        {
            return notice;
        }

        await userTokenIssuer.InvalidateAsync(user.Id, UserTokenPurpose.PasswordReset, cancellationToken);
        user.SetPassword(passwordHasher.Hash(command.NewPassword));

        IReadOnlyList<RefreshToken> sessions = await refreshTokenStore.GetActiveForUserAsync(user.Id, utcNow, cancellationToken);
        var otherSessions = sessions.Where(s => s.FamilyId != tenantContext.CurrentSessionId).ToList();

        foreach (RefreshToken session in otherSessions)
        {
            session.Revoke(utcNow);
        }

        auditLog.Record(new AuditRecord(UserAuditActions.PasswordChanged, nameof(User), user.Id.ToString())
        {
            Metadata = new Dictionary<string, string>
            {
                ["revokedSessions"] = otherSessions.Count.ToString(CultureInfo.InvariantCulture)
            }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(notice.Value, utcNow.Add(UserTokenPolicy.NoticeDeliveryWindow), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
