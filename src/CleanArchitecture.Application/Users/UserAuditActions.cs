namespace CleanArchitecture.Application.Users;

/// <summary>Stable audit action names. Treat them as a persisted contract.</summary>
public static class UserAuditActions
{
    public const string Created = "users.created";
    public const string RoleChanged = "users.role_changed";
    public const string Deactivated = "users.deactivated";
    public const string Activated = "users.activated";
    public const string AdminBootstrapped = "users.admin_bootstrapped";
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string RefreshTokenRotated = "auth.refresh_token_rotated";
    public const string RefreshTokenReuseDetected = "auth.refresh_token_reuse_detected";
    public const string LoggedOut = "auth.logged_out";
}
