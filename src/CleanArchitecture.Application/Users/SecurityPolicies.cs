namespace CleanArchitecture.Application.Users;

internal static class RefreshTokenPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
}

internal static class LockoutPolicy
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
}

internal static class UserTokenPolicy
{
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(3);

    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    /// <summary>How long a security notice (e.g. "your password was changed") may wait in the outbox.</summary>
    public static readonly TimeSpan NoticeDeliveryWindow = TimeSpan.FromDays(1);
}
