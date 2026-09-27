using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Unlock;

/// <param name="TenantId">The user's tenant (admins only); null means the caller's own tenant.</param>
public sealed record UnlockUserCommand(Guid? TenantId, Guid UserId) : ICommand;
