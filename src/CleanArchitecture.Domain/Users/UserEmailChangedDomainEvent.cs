using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserEmailChangedDomainEvent(Guid UserId) : IDomainEvent;
