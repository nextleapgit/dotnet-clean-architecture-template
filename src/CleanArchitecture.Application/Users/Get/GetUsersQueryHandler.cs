using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Get;

internal sealed class GetUsersQueryHandler(IUserStore userStore, TenantAccess tenantAccess)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserResponse>>
{
    public async Task<Result<PagedResponse<UserResponse>>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        Result<TenantId> tenant = await tenantAccess.ResolveAsync(query.TenantId, cancellationToken);

        if (tenant.IsFailure)
        {
            return Result.Failure<PagedResponse<UserResponse>>(tenant.Error);
        }

        return await userStore.ListResponsesAsync(tenant.Value, query.Page, query.PageSize, cancellationToken);
    }
}
