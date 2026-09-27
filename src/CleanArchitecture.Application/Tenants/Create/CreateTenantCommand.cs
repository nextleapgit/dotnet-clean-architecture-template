using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Tenants.Create;

public sealed record CreateTenantCommand(string Name) : ICommand<Guid>;
