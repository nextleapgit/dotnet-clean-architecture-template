using CleanArchitecture.BuildingBlocks.Persistence;

namespace CleanArchitecture.UnitTests.Fakes;

/// <summary>Records saves and transaction boundaries so tests can assert atomicity.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public int SavesInsideTransaction { get; private set; }

    public bool HasActiveTransaction { get; private set; }

    public bool Committed { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCount++;

        if (HasActiveTransaction)
        {
            SavesInsideTransaction++;
        }

        return Task.CompletedTask;
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        HasActiveTransaction = true;

        return Task.FromResult<IUnitOfWorkTransaction>(new Transaction(this));
    }

    private sealed class Transaction(FakeUnitOfWork owner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            owner.Committed = true;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            owner.HasActiveTransaction = false;

            return ValueTask.CompletedTask;
        }
    }
}
