namespace CleanArchitecture.BuildingBlocks.Persistence;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts an explicit transaction on this unit of work. Disposing it without
    /// <see cref="IUnitOfWorkTransaction.CommitAsync"/> rolls back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
