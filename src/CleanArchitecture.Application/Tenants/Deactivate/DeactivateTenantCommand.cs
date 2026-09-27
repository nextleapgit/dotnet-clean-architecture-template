using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Tenants.Deactivate;

public sealed record DeactivateTenantCommand(Guid TenantId) : ICommand;
