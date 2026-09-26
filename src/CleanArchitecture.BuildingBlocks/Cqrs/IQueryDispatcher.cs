using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Cqrs;

public interface IQueryDispatcher
{
    Task<Result<TResponse>> DispatchAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse>;
}
