using CleanArchitecture.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Database;

internal sealed class UnitOfWork(ApplicationDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintViolationException(exception);
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        return new UnitOfWorkTransaction(transaction);
    }

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        // Disposing an uncommitted EF transaction rolls it back.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
