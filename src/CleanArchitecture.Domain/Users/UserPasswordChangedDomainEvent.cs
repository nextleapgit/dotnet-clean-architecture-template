using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserPasswordChangedDomainEvent(Guid UserId) : IDomainEvent;
