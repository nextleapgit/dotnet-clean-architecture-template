using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Application.Tenants.GetById;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Endpoints.Tenants;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tenants/{tenantId:guid}", async (
            Guid tenantId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<TenantResponse> result = await dispatcher.DispatchAsync<GetTenantByIdQuery, TenantResponse>(
                new GetTenantByIdQuery(tenantId),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tenants)
        .HasPermission(Permissions.TenantsRead);
    }
}
