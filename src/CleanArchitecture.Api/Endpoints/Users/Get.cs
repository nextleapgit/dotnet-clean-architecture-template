using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Get;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users", (
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = GetUsersQuery.DefaultPageSize) =>
            HandleAsync(new GetUsersQuery(page, pageSize), dispatcher, cancellationToken))
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersRead);

        app.MapGet("tenants/{tenantId:guid}/users", (
            Guid tenantId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = GetUsersQuery.DefaultPageSize) =>
            HandleAsync(new GetUsersQuery(page, pageSize, tenantId), dispatcher, cancellationToken))
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsRead);
    }

    private static async Task<IResult> HandleAsync(
        GetUsersQuery query,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result<PagedResponse<UserResponse>> result =
            await dispatcher.DispatchAsync<GetUsersQuery, PagedResponse<UserResponse>>(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
