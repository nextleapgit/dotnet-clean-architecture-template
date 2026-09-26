using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos;

internal static class TodoCacheKeys
{
    // The tenant is part of every key so cached data can never cross tenants.
    internal static string ById(TenantId tenantId, Guid userId, Guid todoItemId) =>
        $"todos-{tenantId}-{userId}-{todoItemId}";
}
