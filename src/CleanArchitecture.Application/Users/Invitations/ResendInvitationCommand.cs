using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Invitations;

/// <param name="TenantId">The user's tenant (admins only); null means the caller's own tenant.</param>
public sealed record ResendInvitationCommand(Guid? TenantId, Guid UserId) : ICommand;
