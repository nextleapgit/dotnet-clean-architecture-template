namespace CleanArchitecture.Application.Users;

/// <summary>Stable audit action names. Treat them as a persisted contract.</summary>
public static class UserAuditActions
{
    public const string Registered = "users.registered";
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string RefreshTokenRotated = "auth.refresh_token_rotated";
    public const string RefreshTokenReuseDetected = "auth.refresh_token_reuse_detected";
    public const string LoggedOut = "auth.logged_out";
}
