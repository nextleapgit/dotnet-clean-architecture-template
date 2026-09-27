using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Unlock;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Unlock : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/unlock", (
            Guid userId,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new UnlockUserCommand(null, userId), dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersWrite);

        app.MapPut("tenants/{tenantId:guid}/users/{userId:guid}/unlock", (
            Guid tenantId,
            Guid userId,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new UnlockUserCommand(tenantId, userId), dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsWrite);
    }

    private static async Task<IResult> HandleAsync(
        UnlockUserCommand command,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result result = await dispatcher.DispatchAsync(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
