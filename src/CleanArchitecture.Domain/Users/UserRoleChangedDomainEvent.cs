using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserRoleChangedDomainEvent(Guid UserId, Role PreviousRole, Role NewRole) : IDomainEvent;
