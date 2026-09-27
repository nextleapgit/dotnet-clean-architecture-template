using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Passwords;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class ForgotPassword : IEndpoint
{
    // Well above the slowest normal path (a transaction, a token, an encrypted email), so a registered
    // email answers no slower than an unknown one.
    private static readonly TimeSpan MinimumResponseTime = TimeSpan.FromSeconds(1);

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
        .RequireRateLimiting(RateLimitingPolicies.Authentication)
        .WithMinimumResponseTime(MinimumResponseTime);
    }
}
