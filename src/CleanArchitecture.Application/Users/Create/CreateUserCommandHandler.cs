using CleanArchitecture.Application.Abstractions.Links;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Create;

internal sealed class CreateUserCommandHandler(
    IUnitOfWork unitOfWork,
    TenantAccess tenantAccess,
    UserManagement userManagement,
    IUserStore userStore,
    UserTokenIssuer userTokenIssuer,
    IClientLinks clientLinks,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox)
    : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        Result<TenantId> tenant = await tenantAccess.ResolveAsync(command.TenantId, cancellationToken);

        if (tenant.IsFailure)
        {
            return Result.Failure<Guid>(tenant.Error);
        }

        Result assignable = await userManagement.EnsureRoleAssignableAsync(command.Role, tenant.Value, cancellationToken);

        if (assignable.IsFailure)
        {
            return Result.Failure<Guid>(assignable.Error);
        }

        string email = User.NormalizeEmail(command.Email);

        if (await userStore.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        // No password: nobody but the user ever knows it. The invitation link lets them choose one.
        var user = User.Create(tenant.Value, email, command.FirstName, command.LastName, passwordHash: null, command.Role);

        // The user, the invitation token, the audit entry, and the queued email commit atomically.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        userStore.Add(user);

        IssuedUserToken invitation = await userTokenIssuer.IssueAsync(
            user.Id,
            UserTokenPurpose.Invitation,
            UserTokenPolicy.InvitationLifetime,
            cancellationToken);

        Result<EmailMessage> invitationEmail = UserEmails.Invitation(
            user,
            clientLinks.AcceptInvitation(invitation.Value),
            invitation.ExpiresAtUtc);

        if (invitationEmail.IsFailure)
        {
            return Result.Failure<Guid>(invitationEmail.Error);
        }

        // The acting user comes from the request context; the tenant is the one the user joins.
        auditLog.Record(new AuditRecord(UserAuditActions.Created, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId,
            NewValues = new { role = user.Role.ToString() }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(invitationEmail.Value, invitation.ExpiresAtUtc, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return user.Id;
    }
}
