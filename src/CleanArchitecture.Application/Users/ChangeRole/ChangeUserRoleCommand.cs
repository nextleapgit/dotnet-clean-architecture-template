using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users.ChangeRole;

/// <param name="TenantId">The user's tenant (admins only); null means the caller's own tenant.</param>
public sealed record ChangeUserRoleCommand(Guid? TenantId, Guid UserId, Role Role) : ICommand;
