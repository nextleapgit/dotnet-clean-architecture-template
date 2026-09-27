using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users.Create;

/// <param name="TenantId">The tenant to add the user to (admins only); null means the caller's own tenant.</param>
public sealed record CreateUserCommand(
    Guid? TenantId,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    Role Role) : ICommand<Guid>;
