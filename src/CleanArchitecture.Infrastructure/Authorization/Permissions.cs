namespace CleanArchitecture.Infrastructure.Authorization;

/// <summary>
/// Permission codes, shaped <c>resource:action</c>. Every endpoint requires one of these
/// unless it is explicitly anonymous. <see cref="RolePermissions"/> grants them per role.
/// </summary>
public static class Permissions
{
    public const string ProfileRead = "profile:read";
    public const string UsersRead = "users:read";
    public const string UsersWrite = "users:write";
    public const string TenantsRead = "tenants:read";
    public const string TenantsWrite = "tenants:write";
    public const string TodosRead = "todos:read";
    public const string TodosWrite = "todos:write";
    public const string SessionsManage = "sessions:manage";
}
