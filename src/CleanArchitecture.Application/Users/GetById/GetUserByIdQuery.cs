using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.GetById;

/// <param name="TenantId">Another tenant to read from (admins only); null means the caller's own tenant.</param>
public sealed record GetUserByIdQuery(Guid UserId, Guid? TenantId = null) : IQuery<UserResponse>;
