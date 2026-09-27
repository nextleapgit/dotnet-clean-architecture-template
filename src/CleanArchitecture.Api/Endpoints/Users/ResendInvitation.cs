using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Invitations;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class ResendInvitation : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/{userId:guid}/invitation", (
            Guid userId,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new ResendInvitationCommand(null, userId), dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersWrite);

        app.MapPost("tenants/{tenantId:guid}/users/{userId:guid}/invitation", (
            Guid tenantId,
            Guid userId,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new ResendInvitationCommand(tenantId, userId), dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsWrite);
    }

    private static async Task<IResult> HandleAsync(
        ResendInvitationCommand command,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result result = await dispatcher.DispatchAsync(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
