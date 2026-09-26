using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserRegisteredDomainEvent(Guid UserId) : IDomainEvent;
