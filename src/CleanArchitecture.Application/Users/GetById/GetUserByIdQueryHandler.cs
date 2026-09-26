using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IUserStore userStore, ICurrentTenantContext tenantContext)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        // Users of other tenants are indistinguishable from users that do not exist.
        UserResponse? user = await userStore.GetResponseAsync(
            tenantContext.CurrentTenantId,
            query.UserId,
            cancellationToken);

        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId))
            : user;
    }
}
