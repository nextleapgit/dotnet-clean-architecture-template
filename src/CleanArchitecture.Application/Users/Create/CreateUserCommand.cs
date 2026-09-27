using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users.Create;

/// <summary>Creates an invited user; the user chooses a password through the emailed invitation link.</summary>
/// <param name="TenantId">The tenant to add the user to (admins only); null means the caller's own tenant.</param>
public sealed record CreateUserCommand(
    Guid? TenantId,
    string Email,
    string FirstName,
    string LastName,
    Role Role) : ICommand<Guid>;
