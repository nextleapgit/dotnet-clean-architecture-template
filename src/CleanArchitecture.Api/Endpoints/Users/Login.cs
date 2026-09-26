using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Login;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Login : IEndpoint
{
    public sealed record Request(string Email, string Password);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/login", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<AccessTokensResponse> result = await dispatcher.DispatchAsync<LoginUserCommand, AccessTokensResponse>(
                new LoginUserCommand(request.Email, request.Password),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
