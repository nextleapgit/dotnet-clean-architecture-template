using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserLockedOutDomainEvent(Guid UserId, DateTime LockoutEndUtc) : IDomainEvent;
