using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserDeactivatedDomainEvent(Guid UserId) : IDomainEvent;
