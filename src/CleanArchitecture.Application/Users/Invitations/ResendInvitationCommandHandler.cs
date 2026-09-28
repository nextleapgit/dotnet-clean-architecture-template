using CleanArchitecture.Application.Abstractions.Links;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Invitations;

internal sealed class ResendInvitationCommandHandler(
    IUnitOfWork unitOfWork,
    UserManagement userManagement,
    UserTokenIssuer userTokenIssuer,
    IClientLinks clientLinks,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox,
    IUserStore userStore)
    : ICommandHandler<ResendInvitationCommand>
{
    public async Task<Result> HandleAsync(ResendInvitationCommand command, CancellationToken cancellationToken)
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

        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await userStore.LockForUpdateAsync(user, cancellationToken);

        if (user.HasPassword)
        {
            return Result.Failure(UserErrors.InvitationAlreadyAccepted);
        }

        // Issuing supersedes the previous invitation link.
        IssuedUserToken invitation = await userTokenIssuer.IssueAsync(
            user.Id,
            UserTokenPurpose.Invitation,
            UserTokenPolicy.InvitationLifetime,
            cancellationToken);

        Result<EmailMessage> email = UserEmails.Invitation(user, clientLinks.AcceptInvitation(invitation.Value), invitation.ExpiresAtUtc);

        if (email.IsFailure)
        {
            return email;
        }

        auditLog.Record(new AuditRecord(UserAuditActions.InvitationResent, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(email.Value, invitation.ExpiresAtUtc, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
