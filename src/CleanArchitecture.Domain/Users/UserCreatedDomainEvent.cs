using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent;
