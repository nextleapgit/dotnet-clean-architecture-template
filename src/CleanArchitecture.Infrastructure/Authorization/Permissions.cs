namespace CleanArchitecture.Infrastructure.Authorization;

/// <summary>
/// Permission codes, shaped <c>resource:action</c>. Every endpoint requires one of these
/// unless it is explicitly anonymous.
/// </summary>
public static class Permissions
{
    public const string UsersRead = "users:read";
    public const string TodosRead = "todos:read";
    public const string TodosWrite = "todos:write";
    public const string SessionsManage = "sessions:manage";
}
