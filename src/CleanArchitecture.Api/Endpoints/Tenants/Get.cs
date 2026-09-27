using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Application.Tenants.Get;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Tenants;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tenants", async (
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = GetTenantsQuery.DefaultPageSize) =>
        {
            Result<PagedResponse<TenantResponse>> result =
                await dispatcher.DispatchAsync<GetTenantsQuery, PagedResponse<TenantResponse>>(
                    new GetTenantsQuery(page, pageSize),
                    cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsRead);
    }
}
