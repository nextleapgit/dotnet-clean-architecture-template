using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Passwords;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class ForgotPassword : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/password/forgot", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(new ForgotPasswordCommand(request.Email), cancellationToken);

            return result.Match(() => Results.Accepted(), CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
