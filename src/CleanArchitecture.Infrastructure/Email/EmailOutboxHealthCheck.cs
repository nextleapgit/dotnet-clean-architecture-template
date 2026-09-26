using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Degraded when a sendable email has waited too long (stopped worker, stuck lease, or failing SMTP).
/// Deliberately not a readiness check: an email backlog must not take the instance out of rotation.
/// </summary>
internal sealed class EmailOutboxHealthCheck(EmailOutboxStore store, IOptions<EmailOutboxOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        EmailOutboxOptions settings = options.Value;

        if (!settings.Enabled)
        {
            return HealthCheckResult.Healthy("The email outbox worker is disabled.");
        }

        double? oldestPendingAge = await store.GetOldestPendingAgeSecondsAsync(settings.MaxAttempts, cancellationToken);

        return oldestPendingAge > settings.HealthBacklogThresholdSeconds
            ? HealthCheckResult.Degraded(
                string.Create(CultureInfo.InvariantCulture, $"The oldest sendable email has waited {oldestPendingAge:F0} seconds."))
            : HealthCheckResult.Healthy();
    }
}
