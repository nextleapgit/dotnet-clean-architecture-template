using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Cqrs;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
