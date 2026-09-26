using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Users.Logout;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Logout : IEndpoint
{
    public sealed record Request(string RefreshToken);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/logout", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(new LogoutUserCommand(request.RefreshToken), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.SessionsManage);
    }
}
