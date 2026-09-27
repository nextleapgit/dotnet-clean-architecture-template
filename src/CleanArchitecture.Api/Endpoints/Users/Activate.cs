using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Activate;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Activate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/activate", (
            Guid userId,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new ActivateUserCommand(null, userId), dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersWrite);

        app.MapPut("tenants/{tenantId:guid}/users/{userId:guid}/activate", (
            Guid tenantId,
            Guid userId,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new ActivateUserCommand(tenantId, userId), dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsWrite);
    }

    private static async Task<IResult> HandleAsync(
        ActivateUserCommand command,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result result = await dispatcher.DispatchAsync(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
