using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Update;

/// <param name="TenantId">The user's tenant (admins only); null means the caller's own tenant.</param>
public sealed record UpdateUserCommand(Guid? TenantId, Guid UserId, string FirstName, string LastName) : ICommand;
