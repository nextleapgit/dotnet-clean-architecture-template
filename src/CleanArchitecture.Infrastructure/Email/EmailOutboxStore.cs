using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Coordinates all workers through the database only: rows are claimed with FOR UPDATE SKIP LOCKED
/// and every outcome is a conditional update that re-checks id, lease, and status. The database
/// clock decides due times, leases, expiry, and retention.
/// </summary>
internal sealed class EmailOutboxStore(ApplicationDbContext dbContext)
{
    private const string Table = Schemas.Default + "." + EmailOutboxMessageConfiguration.TableName;
    private const string Now = "date_trunc('milliseconds', now())";

    private const string ClaimSql =
        "UPDATE " + Table + " AS m " +
        "SET status = 1, lease_id = {0}, lease_expires_at_utc = " + Now + " + make_interval(secs => {1}), " +
        "attempt_count = m.attempt_count + 1 " +
        "WHERE m.id = (" +
        "SELECT c.id FROM " + Table + " AS c " +
        "WHERE c.expires_at_utc > now() AND c.attempt_count < {2} " +
        "AND ((c.status = 0 AND c.next_attempt_at_utc <= now()) " +
        "OR (c.status = 1 AND c.lease_expires_at_utc <= now())) " +
        "ORDER BY c.next_attempt_at_utc LIMIT 1 FOR UPDATE SKIP LOCKED) " +
        // Column names follow the snake_case convention EF applies to ClaimedEmail.
        "RETURNING m.id, m.payload, m.expires_at_utc, m.attempt_count";

    private const string OwnedLease = " WHERE id = {0} AND lease_id = {1} AND status = 1";

    private const string MarkSentSql =
        "UPDATE " + Table + " SET status = 2, payload = NULL, lease_id = NULL, lease_expires_at_utc = NULL, " +
        "processed_at_utc = " + Now + ", error_code = NULL" + OwnedLease;

    private const string ScheduleRetrySql =
        "UPDATE " + Table + " SET status = 0, lease_id = NULL, lease_expires_at_utc = NULL, " +
        "next_attempt_at_utc = " + Now + " + make_interval(secs => {2}), error_code = {3}" + OwnedLease;

    private const string MarkFailedSql =
        "UPDATE " + Table + " SET status = 3, payload = NULL, lease_id = NULL, lease_expires_at_utc = NULL, " +
        "processed_at_utc = " + Now + ", error_code = {2}" + OwnedLease;

    // Rows being sent under a live lease are left alone until their lease runs out.
    private const string ExpireSql =
        "UPDATE " + Table + " SET status = 4, payload = NULL, lease_id = NULL, lease_expires_at_utc = NULL, " +
        "processed_at_utc = " + Now + ", error_code = 'delivery_window_elapsed' " +
        "WHERE id IN (SELECT id FROM " + Table + " " +
        "WHERE expires_at_utc <= now() AND (status = 0 OR (status = 1 AND lease_expires_at_utc <= now())) " +
        "LIMIT {0} FOR UPDATE SKIP LOCKED)";

    private const string FailExhaustedSql =
        "UPDATE " + Table + " SET status = 3, payload = NULL, lease_id = NULL, lease_expires_at_utc = NULL, " +
        "processed_at_utc = " + Now + ", error_code = 'attempts_exhausted' " +
        "WHERE id IN (SELECT id FROM " + Table + " " +
        "WHERE attempt_count >= {1} AND (status = 0 OR (status = 1 AND lease_expires_at_utc <= now())) " +
        "LIMIT {0} FOR UPDATE SKIP LOCKED)";

    private const string CleanupSql =
        "DELETE FROM " + Table + " WHERE id IN (SELECT id FROM " + Table + " " +
        "WHERE status IN (2, 3, 4) AND processed_at_utc < now() - make_interval(days => {1}) " +
        "LIMIT {0} FOR UPDATE SKIP LOCKED)";

    private const string BacklogSql =
        "SELECT EXTRACT(EPOCH FROM now() - min(next_attempt_at_utc))::double precision AS \"Value\" " +
        "FROM " + Table + " WHERE (status = 0 AND next_attempt_at_utc <= now()) " +
        "OR (status = 1 AND lease_expires_at_utc <= now())";

    private const string DeliveryFailuresSql =
        "SELECT count(*) AS \"Value\" FROM " + Table +
        " WHERE status IN (3, 4) AND processed_at_utc > now() - make_interval(mins => {0})";

    public async Task<ClaimedEmail?> ClaimNextAsync(
        Guid leaseId,
        int leaseSeconds,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        List<ClaimedEmail> claimed = await dbContext.Database
            .SqlQueryRaw<ClaimedEmail>(ClaimSql, leaseId, leaseSeconds, maxAttempts)
            .ToListAsync(cancellationToken);

        return claimed.SingleOrDefault();
    }

    public async Task<bool> MarkSentAsync(Guid id, Guid leaseId, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(MarkSentSql, [id, leaseId], cancellationToken) == 1;

    public async Task<bool> ScheduleRetryAsync(
        Guid id,
        Guid leaseId,
        int delaySeconds,
        string errorCode,
        CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(
            ScheduleRetrySql,
            [id, leaseId, delaySeconds, errorCode],
            cancellationToken) == 1;

    public async Task<bool> MarkFailedAsync(
        Guid id,
        Guid leaseId,
        string errorCode,
        CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(MarkFailedSql, [id, leaseId, errorCode], cancellationToken) == 1;

    /// <summary>Moves expired and attempt-exhausted messages to their terminal states.</summary>
    public async Task<int> SettleAsync(int batchSize, int maxAttempts, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(ExpireSql, [batchSize], cancellationToken) +
        await dbContext.Database.ExecuteSqlRawAsync(FailExhaustedSql, [batchSize, maxAttempts], cancellationToken);

    public Task<int> DeleteTerminalAsync(int batchSize, int retentionDays, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(CleanupSql, [batchSize, retentionDays], cancellationToken);

    /// <summary>Seconds the oldest claimable pending message has been waiting, or null when none waits.</summary>
    public async Task<double?> GetOldestPendingAgeSecondsAsync(CancellationToken cancellationToken) =>
        await dbContext.Database.SqlQueryRaw<double?>(BacklogSql).SingleAsync(cancellationToken);

    /// <summary>Emails that failed or expired within the last <paramref name="windowMinutes"/>.</summary>
    public Task<long> GetRecentDeliveryFailuresAsync(int windowMinutes, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQueryRaw<long>(DeliveryFailuresSql, windowMinutes).SingleAsync(cancellationToken);
}

internal sealed class ClaimedEmail
{
    public Guid Id { get; init; }
    public byte[] Payload { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public int AttemptCount { get; init; }
}
