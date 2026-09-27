using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants.Create;

internal sealed class CreateTenantCommandHandler(
    IUnitOfWork unitOfWork,
    TenantAccess tenantAccess,
    ITenantStore tenantStore,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog)
    : ICommandHandler<CreateTenantCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        if (!await tenantAccess.IsAdminAsync(cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.Forbidden);
        }

        var tenant = Tenant.Create(command.Name, dateTimeProvider.UtcNow);

        tenantStore.Add(tenant);

        auditLog.Record(new AuditRecord(TenantAuditActions.Created, nameof(Tenant), tenant.Id.ToString())
        {
            TenantId = tenant.Id,
            NewValues = new { name = tenant.Name }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return tenant.Id.Value;
    }
}
