using CleanArchitecture.Application.Abstractions.Authentication;
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
    IUserStore userStore,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox)
    : ICommandHandler<CreateUserCommand, Guid>
{
    private static readonly TimeSpan WelcomeEmailDeliveryWindow = TimeSpan.FromDays(3);

    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        Result<TenantId> tenant = await tenantAccess.ResolveAsync(command.TenantId, cancellationToken);

        if (tenant.IsFailure)
        {
            return Result.Failure<Guid>(tenant.Error);
        }

        string email = User.NormalizeEmail(command.Email);

        if (await userStore.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var user = User.Create(
            tenant.Value,
            email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password),
            command.Role);

        Result<EmailMessage> welcomeEmail = WelcomeEmail.Create(user);

        if (welcomeEmail.IsFailure)
        {
            return Result.Failure<Guid>(welcomeEmail.Error);
        }

        // The user, its audit entry, and the queued email commit atomically.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        userStore.Add(user);

        // The acting user comes from the request context; the tenant is the one the user joins.
        auditLog.Record(new AuditRecord(UserAuditActions.Created, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId,
            NewValues = new { role = user.Role.ToString() }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(
            welcomeEmail.Value,
            dateTimeProvider.UtcNow.Add(WelcomeEmailDeliveryWindow),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return user.Id;
    }
}
