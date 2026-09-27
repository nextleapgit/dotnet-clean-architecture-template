using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants.Activate;

internal sealed class ActivateTenantCommandHandler(
    IUnitOfWork unitOfWork,
    TenantAccess tenantAccess,
    ITenantStore tenantStore,
    IAuditLog auditLog)
    : ICommandHandler<ActivateTenantCommand>
{
    public async Task<Result> HandleAsync(ActivateTenantCommand command, CancellationToken cancellationToken)
    {
        if (!await tenantAccess.IsAdminAsync(cancellationToken))
        {
            return Result.Failure(UserErrors.Forbidden);
        }

        Tenant? tenant = await tenantStore.FindAsync(new TenantId(command.TenantId), cancellationToken);

        if (tenant is null)
        {
            return Result.Failure(TenantErrors.NotFound(command.TenantId));
        }

        if (tenant.IsActive)
        {
            return Result.Success();
        }

        tenant.Activate();

        auditLog.Record(new AuditRecord(TenantAuditActions.Activated, nameof(Tenant), tenant.Id.ToString())
        {
            TenantId = tenant.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
