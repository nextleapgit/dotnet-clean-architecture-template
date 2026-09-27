using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Tenants.Activate;

public sealed record ActivateTenantCommand(Guid TenantId) : ICommand;
