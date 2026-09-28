using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Users;

/// <summary>
/// The one lock that serializes everything touching a user's credentials and sessions: sign-in,
/// refresh, logout, password change and reset, emailed tokens, and deactivation. Always the user's
/// row first, so these operations cannot deadlock one another.
/// </summary>
internal static class UserRowLock
{
    private const string Sql = "SELECT 1 FROM " + Schemas.Default + ".users WHERE id = {0} FOR UPDATE";

    /// <summary>Waits for the lock and holds it until the current transaction ends.</summary>
    /// <exception cref="InvalidOperationException">Outside a transaction, where the lock would end at once.</exception>
    public static async Task AcquireAsync(ApplicationDbContext dbContext, Guid userId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A user row lock lasts until the transaction ends, so it requires one.");
        }

        await dbContext.Database.ExecuteSqlRawAsync(Sql, [userId], cancellationToken);
    }
}
