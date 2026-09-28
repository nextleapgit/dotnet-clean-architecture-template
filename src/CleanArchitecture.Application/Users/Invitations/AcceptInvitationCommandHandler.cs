using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Invitations;

internal sealed class AcceptInvitationCommandHandler(
    IUnitOfWork unitOfWork,
    UserTokenIssuer userTokenIssuer,
    ITenantStore tenantStore,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog)
    : ICommandHandler<AcceptInvitationCommand>
{
    public async Task<Result> HandleAsync(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        UserToken? invitation = await userTokenIssuer.FindUsableAsync(
            command.Token,
            UserTokenPurpose.Invitation,
            cancellationToken);

        // Every reason the link cannot be used gets the same answer.
        if (invitation is null
            || invitation.User.HasPassword
            || !invitation.User.IsActive
            || !await tenantStore.IsActiveAsync(invitation.User.TenantId, cancellationToken))
        {
            return Result.Failure(UserErrors.InvalidOrExpiredToken);
        }

        User user = invitation.User;

        user.SetPassword(passwordHasher.Hash(command.Password));
        invitation.Consume(dateTimeProvider.UtcNow);

        auditLog.Record(new AuditRecord(UserAuditActions.InvitationAccepted, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId,
            UserId = user.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
