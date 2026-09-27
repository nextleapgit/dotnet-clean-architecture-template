using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserUpdatedDomainEvent(Guid UserId) : IDomainEvent;
