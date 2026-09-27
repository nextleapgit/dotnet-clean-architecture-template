using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Tenants;

public static class TenantErrors
{
    public static Error NotFound(Guid tenantId) => Error.NotFound(
        "Tenants.NotFound",
        $"The tenant with the Id = '{tenantId}' was not found");

    public static readonly Error CannotDeactivateOwnTenant = Error.Problem(
        "Tenants.CannotDeactivateOwnTenant",
        "You cannot deactivate the tenant you belong to.");
}
