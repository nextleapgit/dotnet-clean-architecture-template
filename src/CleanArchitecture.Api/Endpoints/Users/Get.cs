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
        app.MapGet("users", async (
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = GetUsersQuery.DefaultPageSize) =>
        {
            Result<PagedResponse<UserResponse>> result =
                await dispatcher.DispatchAsync<GetUsersQuery, PagedResponse<UserResponse>>(
                    new GetUsersQuery(page, pageSize),
                    cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersRead);
    }
}
