using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Users;

/// <summary>Deletes ended refresh-token families and used or expired emailed tokens. Validated at start-up.</summary>
internal sealed class TokenCleanupOptions
{
    public const string SectionName = "TokenCleanup";

    /// <summary>On by default: it needs nothing but the database, and every instance may run it.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// How long a token is kept after it ended (used, revoked, or expired). While a refresh-token
    /// family is kept, replaying one of its rotated tokens is still reported as reuse.
    /// </summary>
    public int RetentionDays { get; init; } = 30;

    public int IntervalMinutes { get; init; } = 60;
    public int BatchSize { get; init; } = 1000;
}

internal sealed class TokenCleanupOptionsValidator : IValidateOptions<TokenCleanupOptions>
{
    public ValidateOptionsResult Validate(string? name, TokenCleanupOptions options)
    {
        List<string> failures = [];

        if (options.RetentionDays <= 0)
        {
            failures.Add("TokenCleanup:RetentionDays must be positive.");
        }

        if (options.IntervalMinutes <= 0)
        {
            failures.Add("TokenCleanup:IntervalMinutes must be positive.");
        }

        if (options.BatchSize <= 0)
        {
            failures.Add("TokenCleanup:BatchSize must be positive.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
