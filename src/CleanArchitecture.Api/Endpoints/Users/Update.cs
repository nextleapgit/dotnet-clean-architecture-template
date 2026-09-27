using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Update;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string FirstName, string LastName);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}", (
            Guid userId,
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new UpdateUserCommand(null, userId, request.FirstName, request.LastName), dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersWrite);

        app.MapPut("tenants/{tenantId:guid}/users/{userId:guid}", (
            Guid tenantId,
            Guid userId,
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new UpdateUserCommand(tenantId, userId, request.FirstName, request.LastName), dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsWrite);
    }

    private static async Task<IResult> HandleAsync(
        UpdateUserCommand command,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result result = await dispatcher.DispatchAsync(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
