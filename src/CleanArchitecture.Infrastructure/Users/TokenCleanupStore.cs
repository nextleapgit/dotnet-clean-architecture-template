using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Users;

/// <summary>
/// Batched deletes of tokens that can never be used again. The database clock decides retention,
/// and concurrent instances may run the same delete safely.
/// </summary>
internal sealed class TokenCleanupStore(ApplicationDbContext dbContext)
{
    private const string RefreshTokens = Schemas.Default + ".refresh_tokens";
    private const string UserTokens = Schemas.Default + ".user_tokens";

    // Whole families only, and only once every token in them ended: a rotated token must stay while
    // its family lives, because replaying it is how a leak is detected (and the family revoked).
    private const string RefreshTokenFamiliesSql =
        "DELETE FROM " + RefreshTokens + " WHERE family_id IN (" +
        "SELECT family_id FROM " + RefreshTokens + " GROUP BY family_id " +
        "HAVING max(COALESCE(revoked_at_utc, expires_on_utc)) < now() - make_interval(days => {1}) " +
        "LIMIT {0})";

    // Used or superseded tokens carry consumed_at_utc; the others end when they expire.
    private const string UserTokensSql =
        "DELETE FROM " + UserTokens + " WHERE id IN (" +
        "SELECT id FROM " + UserTokens + " " +
        "WHERE COALESCE(consumed_at_utc, expires_at_utc) < now() - make_interval(days => {1}) " +
        "LIMIT {0} FOR UPDATE SKIP LOCKED)";

    /// <returns>The number of deleted refresh tokens (at most <paramref name="familyBatchSize"/> families).</returns>
    public Task<int> DeleteEndedRefreshTokenFamiliesAsync(int familyBatchSize, int retentionDays, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(RefreshTokenFamiliesSql, [familyBatchSize, retentionDays], cancellationToken);

    /// <returns>The number of deleted emailed tokens.</returns>
    public Task<int> DeleteEndedUserTokensAsync(int batchSize, int retentionDays, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(UserTokensSql, [batchSize, retentionDays], cancellationToken);
}
