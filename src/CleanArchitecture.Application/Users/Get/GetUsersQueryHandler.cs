using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Get;

internal sealed class GetUsersQueryHandler(IUserStore userStore, ICurrentTenantContext tenantContext)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserResponse>>
{
    public async Task<Result<PagedResponse<UserResponse>>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken) =>
        await userStore.ListResponsesAsync(
            tenantContext.CurrentTenantId,
            query.Page,
            query.PageSize,
            cancellationToken);
}
