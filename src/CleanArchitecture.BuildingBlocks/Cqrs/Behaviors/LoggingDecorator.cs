using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace CleanArchitecture.BuildingBlocks.Cqrs.Behaviors;

internal static class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
        {
            string commandName = typeof(TCommand).Name;

            logger.LogInformation("Processing command {Command}", commandName);

            Result<TResponse> result = await innerHandler.HandleAsync(command, cancellationToken);

            LogCompletion(logger, "command", commandName, result);

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken)
        {
            string commandName = typeof(TCommand).Name;

            logger.LogInformation("Processing command {Command}", commandName);

            Result result = await innerHandler.HandleAsync(command, cancellationToken);

            LogCompletion(logger, "command", commandName, result);

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken)
        {
            string queryName = typeof(TQuery).Name;

            logger.LogInformation("Processing query {Query}", queryName);

            Result<TResponse> result = await innerHandler.HandleAsync(query, cancellationToken);

            LogCompletion(logger, "query", queryName, result);

            return result;
        }
    }

    private static void LogCompletion(ILogger logger, string kind, string name, Result result)
    {
        if (result.IsSuccess)
        {
            logger.LogInformation("Completed {Kind} {Name}", kind, name);
            return;
        }

        // Only the stable error code is logged; descriptions may contain user input.
        using (LogContext.PushProperty("ErrorCode", result.Error.Code))
        {
            logger.LogWarning("Completed {Kind} {Name} with error {ErrorCode}", kind, name, result.Error.Code);
        }
    }
}
