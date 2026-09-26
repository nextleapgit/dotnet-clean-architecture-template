namespace CleanArchitecture.Application.Users;

internal static class RefreshTokenPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
}
