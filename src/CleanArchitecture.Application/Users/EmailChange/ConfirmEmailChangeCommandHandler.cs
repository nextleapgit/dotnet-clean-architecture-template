using System.Globalization;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.EmailChange;

internal sealed class ConfirmEmailChangeCommandHandler(
    IUnitOfWork unitOfWork,
    IUserStore userStore,
    UserTokenIssuer userTokenIssuer,
    ITenantStore tenantStore,
    IRefreshTokenStore refreshTokenStore,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog)
    : ICommandHandler<ConfirmEmailChangeCommand>
{
    public async Task<Result> HandleAsync(ConfirmEmailChangeCommand command, CancellationToken cancellationToken)
    {
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Holds the user's row lock from here on, so a concurrent change of credentials waits.
        UserToken? link = await userTokenIssuer.FindUsableAsync(command.Token, UserTokenPurpose.EmailChange, cancellationToken);

        // Every reason the link cannot be used gets the same answer.
        if (link?.Payload is null
            || !link.User.IsActive
            || !await tenantStore.IsActiveAsync(link.User.TenantId, cancellationToken))
        {
            return Result.Failure(UserErrors.InvalidOrExpiredToken);
        }

        User user = link.User;
        string newEmail = link.Payload;

        // The address may have been taken since the link was sent. (A race past this check hits the
        // unique index and becomes a 409 as well.)
        if (newEmail != user.Email && await userStore.EmailExistsAsync(newEmail, cancellationToken))
        {
            return Result.Failure(UserErrors.EmailNotUnique);
        }

        DateTime utcNow = dateTimeProvider.UtcNow;

        user.ChangeEmail(newEmail);

        // The sign-in name changed: pending links of either kind and every session end with it.
        await userTokenIssuer.InvalidateAsync(user.Id, UserTokenPurpose.EmailChange, cancellationToken);
        await userTokenIssuer.InvalidateAsync(user.Id, UserTokenPurpose.PasswordReset, cancellationToken);

        IReadOnlyList<RefreshToken> sessions = await refreshTokenStore.GetActiveForUserAsync(user.Id, utcNow, cancellationToken);

        foreach (RefreshToken session in sessions)
        {
            session.Revoke(utcNow);
        }

        auditLog.Record(new AuditRecord(UserAuditActions.EmailChanged, nameof(User), user.Id.ToString(), AuditSeverity.Warning)
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
