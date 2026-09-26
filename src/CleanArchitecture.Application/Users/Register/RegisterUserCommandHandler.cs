using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IUnitOfWork unitOfWork,
    ITenantStore tenantStore,
    IUserStore userStore,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog,
    IEmailOutbox emailOutbox)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    private static readonly TimeSpan WelcomeEmailDeliveryWindow = TimeSpan.FromDays(3);

    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        string email = User.NormalizeEmail(command.Email);

        if (await userStore.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        DateTime utcNow = dateTimeProvider.UtcNow;

        // Every sign-up starts its own tenant; inviting users into an existing tenant is a project feature.
        var tenant = Tenant.Create($"{command.FirstName} {command.LastName}", utcNow);

        var user = User.Create(
            tenant.Id,
            email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        Result<EmailMessage> welcomeEmail = WelcomeEmail.Create(user);

        if (welcomeEmail.IsFailure)
        {
            return Result.Failure<Guid>(welcomeEmail.Error);
        }

        // The user, its audit entry, and the queued email commit atomically.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        tenantStore.Add(tenant);
        userStore.Add(user);

        auditLog.Record(new AuditRecord(UserAuditActions.Registered, nameof(User), user.Id.ToString())
        {
            TenantId = tenant.Id,
            UserId = user.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailOutbox.EnqueueAsync(welcomeEmail.Value, utcNow.Add(WelcomeEmailDeliveryWindow), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return user.Id;
    }
}
