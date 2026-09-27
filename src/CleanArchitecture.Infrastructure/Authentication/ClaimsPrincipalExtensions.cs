using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Infrastructure.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId([NotNullWhen(true)] this ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;

        return principal is not null &&
               Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    public static bool TryGetTenantId([NotNullWhen(true)] this ClaimsPrincipal? principal, out TenantId tenantId)
    {
        tenantId = default;

        if (principal is null || !Guid.TryParse(principal.FindFirstValue(CustomClaimNames.TenantId), out Guid value))
        {
            return false;
        }

        tenantId = new TenantId(value);

        return true;
    }

    public static bool TryGetSessionId([NotNullWhen(true)] this ClaimsPrincipal? principal, out Guid sessionId)
    {
        sessionId = Guid.Empty;

        return principal is not null &&
               Guid.TryParse(principal.FindFirstValue(CustomClaimNames.SessionId), out sessionId);
    }
}
