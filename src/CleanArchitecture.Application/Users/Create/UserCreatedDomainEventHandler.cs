using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Application.Users.Create;

internal sealed class UserCreatedDomainEventHandler(ILogger<UserCreatedDomainEventHandler> logger)
    : IDomainEventHandler<UserCreatedDomainEvent>
{
    public Task Handle(UserCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("User {UserId} created", domainEvent.UserId);

        return Task.CompletedTask;
    }
}
