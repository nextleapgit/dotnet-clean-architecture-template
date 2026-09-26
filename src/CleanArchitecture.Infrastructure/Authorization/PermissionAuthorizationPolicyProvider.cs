using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Authorization;

internal sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicy? policy = await base.GetPolicyAsync(policyName);

        // Building a policy is cheap, so we avoid mutating the shared AuthorizationOptions,
        // which is not safe under concurrent requests.
        return policy ?? new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
