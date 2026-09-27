using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.GetCurrent;

internal sealed class GetCurrentUserQueryHandler(IUserStore userStore, ICurrentTenantContext tenantContext)
    : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        // A valid token can outlive its user; that is reported as NotFound, not as an exception.
        UserResponse? user = await userStore.GetResponseAsync(
            tenantContext.CurrentTenantId,
            tenantContext.CurrentUserId,
            cancellationToken);

        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound(tenantContext.CurrentUserId))
            : user;
    }
}
