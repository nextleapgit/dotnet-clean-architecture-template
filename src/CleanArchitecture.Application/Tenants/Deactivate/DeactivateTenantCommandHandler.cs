using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Tenants.Deactivate;

internal sealed class DeactivateTenantCommandHandler(
    IUnitOfWork unitOfWork,
    TenantAccess tenantAccess,
    ITenantStore tenantStore,
    ICurrentTenantContext tenantContext,
    IAuditLog auditLog)
    : ICommandHandler<DeactivateTenantCommand>
{
    public async Task<Result> HandleAsync(DeactivateTenantCommand command, CancellationToken cancellationToken)
    {
        if (!await tenantAccess.IsAdminAsync(cancellationToken))
        {
            return Result.Failure(UserErrors.Forbidden);
        }

        // Deactivating the platform tenant would lock every admin out.
        if (command.TenantId == tenantContext.CurrentTenantId.Value)
        {
            return Result.Failure(TenantErrors.CannotDeactivateOwnTenant);
        }

        Tenant? tenant = await tenantStore.FindAsync(new TenantId(command.TenantId), cancellationToken);

        if (tenant is null)
        {
            return Result.Failure(TenantErrors.NotFound(command.TenantId));
        }

        if (!tenant.IsActive)
        {
            return Result.Success();
        }

        tenant.Deactivate();

        auditLog.Record(new AuditRecord(TenantAuditActions.Deactivated, nameof(Tenant), tenant.Id.ToString(), AuditSeverity.Warning)
        {
            TenantId = tenant.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
