using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Links;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.EmailChange;

internal sealed class RequestEmailChangeCommandHandler(
    IUnitOfWork unitOfWork,
    IUserStore userStore,
    UserTokenIssuer userTokenIssuer,
    IPasswordHasher passwordHasher,
    IClientLinks clientLinks,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox)
    : ICommandHandler<RequestEmailChangeCommand>
{
    public async Task<Result> HandleAsync(RequestEmailChangeCommand command, CancellationToken cancellationToken)
    {
        User? user = await userStore.FindAsync(tenantContext.CurrentTenantId, tenantContext.CurrentUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(tenantContext.CurrentUserId));
        }

        // As for a password change: attempts run one after another and failures count towards lockout.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await userStore.LockForUpdateAsync(user, cancellationToken);

        DateTime utcNow = dateTimeProvider.UtcNow;

        if (user.IsLockedOut(utcNow))
        {
            return Result.Failure(UserErrors.LockedOut);
        }

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

        string newEmail = User.NormalizeEmail(command.NewEmail);

        if (newEmail == user.Email)
        {
            return Result.Failure(UserErrors.EmailUnchanged);
        }

        Result<EmailMessage> notice = UserEmails.EmailChangeRequested(user);

        if (notice.IsFailure)
        {
            return notice;
        }

        // The answer must not reveal whether the address belongs to another account, so a taken address
        // gets the same response — but no link. Confirmation checks uniqueness again in any case.
        EmailMessage? confirmation = null;
        DateTime? confirmationExpiresAtUtc = null;

        if (!await userStore.EmailExistsAsync(newEmail, cancellationToken))
        {
            // Issuing supersedes any earlier email-change link.
            IssuedUserToken link = await userTokenIssuer.IssueAsync(
                user.Id,
                UserTokenPurpose.EmailChange,
                UserTokenPolicy.EmailChangeLifetime,
                cancellationToken,
                payload: newEmail);

            Result<EmailMessage> email = UserEmails.EmailChangeConfirmation(
                user,
                newEmail,
                clientLinks.ConfirmEmailChange(link.Value),
                link.ExpiresAtUtc);

            if (email.IsFailure)
            {
                return email;
            }

            confirmation = email.Value;
            confirmationExpiresAtUtc = link.ExpiresAtUtc;
        }

        // Neither address goes into the audit trail; the entry records that a change was requested.
        auditLog.Record(new AuditRecord(UserAuditActions.EmailChangeRequested, nameof(User), user.Id.ToString()));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(notice.Value, utcNow.Add(UserTokenPolicy.NoticeDeliveryWindow), cancellationToken);

        if (confirmation is not null)
        {
            await emailOutbox.EnqueueAsync(confirmation, confirmationExpiresAtUtc!.Value, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
