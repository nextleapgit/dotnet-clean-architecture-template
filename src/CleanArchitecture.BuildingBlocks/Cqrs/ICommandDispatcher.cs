using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Cqrs;

public interface ICommandDispatcher
{
    Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : ICommand;

    Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse>;
}
