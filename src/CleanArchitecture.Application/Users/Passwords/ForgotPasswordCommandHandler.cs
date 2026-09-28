using CleanArchitecture.Application.Abstractions.Links;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Passwords;

internal sealed class ForgotPasswordCommandHandler(
    IUnitOfWork unitOfWork,
    IUserStore userStore,
    ITenantStore tenantStore,
    UserTokenIssuer userTokenIssuer,
    IClientLinks clientLinks,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox)
    : ICommandHandler<ForgotPasswordCommand>
{
    public async Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await userStore.FindByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        // Unknown, invited (no password yet), or disabled accounts get no email — and the same answer.
        if (user is null
            || !user.HasPassword
            || !user.IsActive
            || !await tenantStore.IsActiveAsync(user.TenantId, cancellationToken))
        {
            return Result.Success();
        }

        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await userStore.LockForUpdateAsync(user, cancellationToken);
        if (!user.HasPassword || !user.IsActive || !await tenantStore.IsActiveAsync(user.TenantId, cancellationToken))
        {
            return Result.Success();
        }

        // Issuing supersedes any earlier reset link.
        IssuedUserToken reset = await userTokenIssuer.IssueAsync(
            user.Id,
            UserTokenPurpose.PasswordReset,
            UserTokenPolicy.PasswordResetLifetime,
            cancellationToken);

        Result<EmailMessage> email = UserEmails.PasswordReset(user, clientLinks.ResetPassword(reset.Value), reset.ExpiresAtUtc);

        if (email.IsFailure)
        {
            return email;
        }

        auditLog.Record(new AuditRecord(UserAuditActions.PasswordResetRequested, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId,
            UserId = user.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(email.Value, reset.ExpiresAtUtc, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
