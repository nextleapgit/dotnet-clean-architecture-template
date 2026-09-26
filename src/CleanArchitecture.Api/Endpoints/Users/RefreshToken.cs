using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Refresh;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class RefreshToken : IEndpoint
{
    public sealed record Request(string RefreshToken);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/refresh-token", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<AccessTokensResponse> result = await dispatcher.DispatchAsync<RefreshTokenCommand, AccessTokensResponse>(
                new RefreshTokenCommand(request.RefreshToken),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
