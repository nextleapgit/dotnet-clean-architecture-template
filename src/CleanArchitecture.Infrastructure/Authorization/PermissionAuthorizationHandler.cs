using CleanArchitecture.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.Authorization;

internal sealed class PermissionAuthorizationHandler(IServiceScopeFactory serviceScopeFactory)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Unauthenticated requests never satisfy a permission; the middleware answers with 401.
        if (context.User is not { Identity.IsAuthenticated: true } || !context.User.TryGetUserId(out Guid userId))
        {
            return;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        PermissionProvider permissionProvider = scope.ServiceProvider.GetRequiredService<PermissionProvider>();

        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userId);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
