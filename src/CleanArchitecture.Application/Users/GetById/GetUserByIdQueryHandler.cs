using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IUserStore userStore, TenantAccess tenantAccess)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        Result<TenantId> tenant = await tenantAccess.ResolveAsync(query.TenantId, cancellationToken);

        if (tenant.IsFailure)
        {
            return Result.Failure<UserResponse>(tenant.Error);
        }

        // Users of other tenants are indistinguishable from users that do not exist.
        UserResponse? user = await userStore.GetResponseAsync(tenant.Value, query.UserId, cancellationToken);

        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId))
            : user;
    }
}
