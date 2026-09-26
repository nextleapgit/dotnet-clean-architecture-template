using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.BuildingBlocks.Cqrs;

internal sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    public Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : ICommand =>
        serviceProvider
            .GetRequiredService<ICommandHandler<TCommand>>()
            .HandleAsync(command, cancellationToken);

    public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse> =>
        serviceProvider
            .GetRequiredService<ICommandHandler<TCommand, TResponse>>()
            .HandleAsync(command, cancellationToken);
}

internal sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    public Task<Result<TResponse>> DispatchAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse> =>
        serviceProvider
            .GetRequiredService<IQueryHandler<TQuery, TResponse>>()
            .HandleAsync(query, cancellationToken);
}
