using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.GetById;

namespace CleanArchitecture.Api.Endpoints.Users;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/{userId:guid}", async (
            Guid userId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<UserResponse> result = await dispatcher.DispatchAsync<GetUserByIdQuery, UserResponse>(
                new GetUserByIdQuery(userId),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersRead);
    }
}
