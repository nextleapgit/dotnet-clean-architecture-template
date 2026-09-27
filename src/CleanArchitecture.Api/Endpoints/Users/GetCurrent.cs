using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.GetCurrent;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class GetCurrent : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me", async (
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<UserResponse> result = await dispatcher.DispatchAsync<GetCurrentUserQuery, UserResponse>(
                new GetCurrentUserQuery(),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersRead);
    }
}
