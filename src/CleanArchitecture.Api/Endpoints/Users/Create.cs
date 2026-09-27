using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users.Create;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Email, string FirstName, string LastName, string Password, Role Role);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users", (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(null, request, dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersWrite);

        app.MapPost("tenants/{tenantId:guid}/users", (
            Guid tenantId,
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(tenantId, request, dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsWrite);
    }

    private static async Task<IResult> HandleAsync(
        Guid? tenantId,
        Request request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            tenantId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.Role);

        Result<Guid> result = await dispatcher.DispatchAsync<CreateUserCommand, Guid>(command, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
