using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.GetById;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/{userId:guid}", (
            Guid userId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new GetUserByIdQuery(userId), dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersRead);

        app.MapGet("tenants/{tenantId:guid}/users/{userId:guid}", (
            Guid tenantId,
            Guid userId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            HandleAsync(new GetUserByIdQuery(userId, tenantId), dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsRead);
    }

    private static async Task<IResult> HandleAsync(
        GetUserByIdQuery query,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result<UserResponse> result = await dispatcher.DispatchAsync<GetUserByIdQuery, UserResponse>(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
