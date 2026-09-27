using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Bootstrap;

internal sealed class BootstrapAdminCommandHandler(
    IUnitOfWork unitOfWork,
    ITenantStore tenantStore,
    IUserStore userStore,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog)
    : ICommandHandler<BootstrapAdminCommand>
{
    public async Task<Result> HandleAsync(BootstrapAdminCommand command, CancellationToken cancellationToken)
    {
        if (await userStore.AdminExistsAsync(cancellationToken))
        {
            return Result.Success();
        }

        string email = User.NormalizeEmail(command.Email);

        if (await userStore.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure(UserErrors.EmailNotUnique);
        }

        var tenant = Tenant.Create(command.TenantName, dateTimeProvider.UtcNow);

        var admin = User.Create(
            tenant.Id,
            email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password),
            Role.Admin);

        tenantStore.Add(tenant);
        userStore.Add(admin);

        auditLog.Record(new AuditRecord(
            UserAuditActions.AdminBootstrapped,
            nameof(User),
            admin.Id.ToString(),
            AuditSeverity.Warning)
        {
            TenantId = tenant.Id,
            UserId = admin.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
