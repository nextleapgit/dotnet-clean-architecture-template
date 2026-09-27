using System.Collections.Frozen;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Infrastructure.Authorization;

/// <summary>
/// The permissions of each role. Each role holds everything the role below it holds, so a role
/// can never manage a permission it does not have itself.
/// </summary>
internal static class RolePermissions
{
    private static readonly string[] Member =
    [
        Permissions.ProfileRead,
        Permissions.ProfileWrite,
        Permissions.TodosRead,
        Permissions.TodosWrite,
        Permissions.SessionsManage
    ];

    private static readonly string[] Manager =
    [
        .. Member,
        Permissions.UsersRead,
        Permissions.UsersWrite
    ];

    private static readonly string[] Admin =
    [
        .. Manager,
        Permissions.TenantsRead,
        Permissions.TenantsWrite
    ];

    private static readonly FrozenDictionary<Role, FrozenSet<string>> ByRole = new Dictionary<Role, FrozenSet<string>>
    {
        [Role.Member] = Member.ToFrozenSet(),
        [Role.Manager] = Manager.ToFrozenSet(),
        [Role.Admin] = Admin.ToFrozenSet()
    }.ToFrozenDictionary();

    public static IReadOnlySet<string> For(Role role) =>
        ByRole.TryGetValue(role, out FrozenSet<string>? permissions) ? permissions : FrozenSet<string>.Empty;
}
