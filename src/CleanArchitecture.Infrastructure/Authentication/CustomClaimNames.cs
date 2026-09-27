namespace CleanArchitecture.Infrastructure.Authentication;

internal static class CustomClaimNames
{
    public const string TenantId = "tenant_id";

    /// <summary>The refresh-token family the access token was issued for.</summary>
    public const string SessionId = "session_id";
}
