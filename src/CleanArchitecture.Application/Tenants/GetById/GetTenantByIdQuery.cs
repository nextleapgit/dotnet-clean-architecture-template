using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Tenants.GetById;

public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<TenantResponse>;
