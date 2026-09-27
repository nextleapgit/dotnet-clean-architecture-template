using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Passwords;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class ChangePassword : IEndpoint
{
    public sealed record Request(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/me/password", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(
                new ChangePasswordCommand(request.CurrentPassword, request.NewPassword),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.ProfileWrite)
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
