using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.EmailChange;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class RequestEmailChange : IEndpoint
{
    public sealed record Request(string CurrentPassword, string NewEmail);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/me/email", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(
                new RequestEmailChangeCommand(request.CurrentPassword, request.NewEmail),
                cancellationToken);

            // Accepted: the change takes effect only once the new address is confirmed.
            return result.Match(() => Results.Accepted(), CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.ProfileWrite)
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
