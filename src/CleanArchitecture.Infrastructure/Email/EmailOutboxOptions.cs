using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>Validated at start-up even when disabled. Changes require a restart.</summary>
internal sealed class EmailOutboxOptions
{
    public const string SectionName = "EmailOutbox";

    /// <summary>Off by default: emails are still queued, but only an enabled worker sends them.</summary>
    public bool Enabled { get; init; }

    public int PollIntervalSeconds { get; init; } = 5;
    public int MaxAttempts { get; init; } = 8;
    public int LeaseSeconds { get; init; } = 120;
    public int CompletionTimeoutSeconds { get; init; } = 10;
    public int RetryBaseDelaySeconds { get; init; } = 30;
    public int RetryMaxDelaySeconds { get; init; } = 3600;
    public int SettlementBatchSize { get; init; } = 100;
    public int CleanupBatchSize { get; init; } = 500;
    public int RetentionDays { get; init; } = 14;
    public int CleanupIntervalMinutes { get; init; } = 60;
    public int HealthBacklogThresholdSeconds { get; init; } = 600;

    /// <summary>
    /// Failed or expired emails within <see cref="HealthFailureWindowMinutes"/> that degrade health.
    /// A single failure (one bad address) is not an outage; a run of them is.
    /// </summary>
    public int HealthFailureThreshold { get; init; } = 5;

    public int HealthFailureWindowMinutes { get; init; } = 60;
}

internal sealed class EmailOutboxOptionsValidator(IOptions<SmtpOptions> smtpOptions)
    : IValidateOptions<EmailOutboxOptions>
{
    // Headroom between the lease and the longest possible attempt (send + recording the outcome).
    private const int LeaseHeadroomSeconds = 5;

    public ValidateOptionsResult Validate(string? name, EmailOutboxOptions options)
    {
        List<string> failures = [];

        void Require(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        Require(options.PollIntervalSeconds > 0, "EmailOutbox:PollIntervalSeconds must be positive.");
        Require(options.MaxAttempts > 0, "EmailOutbox:MaxAttempts must be positive.");
        Require(options.CompletionTimeoutSeconds > 0, "EmailOutbox:CompletionTimeoutSeconds must be positive.");
        Require(options.RetryBaseDelaySeconds > 0, "EmailOutbox:RetryBaseDelaySeconds must be positive.");
        Require(
            options.RetryMaxDelaySeconds >= options.RetryBaseDelaySeconds,
            "EmailOutbox:RetryMaxDelaySeconds must not be less than RetryBaseDelaySeconds.");
        Require(options.SettlementBatchSize > 0, "EmailOutbox:SettlementBatchSize must be positive.");
        Require(options.CleanupBatchSize > 0, "EmailOutbox:CleanupBatchSize must be positive.");
        Require(options.RetentionDays > 0, "EmailOutbox:RetentionDays must be positive.");
        Require(options.CleanupIntervalMinutes > 0, "EmailOutbox:CleanupIntervalMinutes must be positive.");
        Require(options.HealthBacklogThresholdSeconds > 0, "EmailOutbox:HealthBacklogThresholdSeconds must be positive.");
        Require(options.HealthFailureThreshold > 0, "EmailOutbox:HealthFailureThreshold must be positive.");
        Require(options.HealthFailureWindowMinutes > 0, "EmailOutbox:HealthFailureWindowMinutes must be positive.");

        int minimumLease = smtpOptions.Value.TimeoutSeconds + options.CompletionTimeoutSeconds + LeaseHeadroomSeconds;
        Require(
            options.LeaseSeconds >= minimumLease,
            $"EmailOutbox:LeaseSeconds must be at least Smtp:TimeoutSeconds + CompletionTimeoutSeconds + {LeaseHeadroomSeconds} ({minimumLease}).");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
