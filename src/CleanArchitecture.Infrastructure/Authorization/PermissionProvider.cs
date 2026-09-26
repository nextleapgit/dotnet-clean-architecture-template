namespace CleanArchitecture.Infrastructure.Authorization;

internal sealed class PermissionProvider
{
    // Permissions granted to every authenticated user.
    private static readonly string[] DefaultPermissions =
    [
        Permissions.UsersRead,
        Permissions.TodosRead,
        Permissions.TodosWrite,
        Permissions.SessionsManage
    ];

    public Task<HashSet<string>> GetForUserIdAsync(Guid userId)
    {
        // TODO: Load role-based permissions for the user once a role/permission model is persisted.
        // A tenant must never be able to grant a permission it does not hold itself.
        HashSet<string> permissionsSet = [.. DefaultPermissions];

        return Task.FromResult(permissionsSet);
    }
}
