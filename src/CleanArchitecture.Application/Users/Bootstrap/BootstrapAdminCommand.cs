using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Bootstrap;

/// <summary>
/// Creates the platform tenant and its first admin when no admin exists yet. Dispatched at
/// start-up from configuration; a no-op once an admin exists.
/// </summary>
public sealed record BootstrapAdminCommand(
    string TenantName,
    string Email,
    string FirstName,
    string LastName,
    string Password) : ICommand;
