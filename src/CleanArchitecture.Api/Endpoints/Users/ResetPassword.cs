using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Passwords;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class ResetPassword : IEndpoint
{
    public sealed record Request(string Token, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/password/reset", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
