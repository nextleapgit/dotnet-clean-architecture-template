using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Claims and sends one message. No database transaction or row lock is held during the SMTP call.
/// </summary>
internal sealed class EmailOutboxDispatcher(
    EmailOutboxStore store,
    EmailPayloadProtector payloadProtector,
    IEmailSender emailSender,
    IOptions<EmailOutboxOptions> outboxOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<EmailOutboxDispatcher> logger)
{
    /// <returns>True when a message was claimed, whatever the outcome of the attempt.</returns>
    public async Task<bool> DispatchOneAsync(CancellationToken cancellationToken)
    {
        EmailOutboxOptions options = outboxOptions.Value;
        var leaseId = Guid.NewGuid();

        ClaimedEmail? claimed = await store.ClaimNextAsync(
            leaseId,
            options.LeaseSeconds,
            options.MaxAttempts,
            cancellationToken);

        if (claimed is null)
        {
            return false;
        }

        Result<EmailMessage> message = payloadProtector.Unprotect(claimed.Id, claimed.ExpiresAtUtc, claimed.Payload);

        EmailSendResult result = message.IsSuccess
            ? await SendAsync(message.Value, cancellationToken)
            : EmailSendResult.PermanentFailure(EmailErrorCodes.PayloadProtectionFailure);

        // Record the outcome even if the host is stopping; the lease guards against double completion.
        using var completionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.CompletionTimeoutSeconds));

        bool recorded = await RecordOutcomeAsync(claimed, leaseId, result, options, completionTimeout.Token);

        logger.LogInformation(
            "Email {EmailId} attempt {Attempt}: {Outcome} {ErrorCode} (recorded: {Recorded})",
            claimed.Id,
            claimed.AttemptCount,
            result.Outcome,
            result.ErrorCode,
            recorded);

        return true;
    }

    private async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(smtpOptions.Value.TimeoutSeconds));

        try
        {
            return await emailSender.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return EmailSendResult.TemporaryFailure(EmailErrorCodes.SmtpTimeout);
        }
        catch (Exception exception)
        {
            // Only the exception type is logged; its message may contain private data.
#pragma warning disable S6667
            logger.LogWarning("Unexpected {ExceptionType} while sending an email", exception.GetType().Name);
#pragma warning restore S6667

            return EmailSendResult.TemporaryFailure(EmailErrorCodes.SmtpTemporaryFailure);
        }
    }

    private Task<bool> RecordOutcomeAsync(
        ClaimedEmail claimed,
        Guid leaseId,
        EmailSendResult result,
        EmailOutboxOptions options,
        CancellationToken cancellationToken) =>
        result.Outcome switch
        {
            EmailSendOutcome.Accepted => store.MarkSentAsync(claimed.Id, leaseId, cancellationToken),
            EmailSendOutcome.PermanentFailure => store.MarkFailedAsync(
                claimed.Id,
                leaseId,
                result.ErrorCode ?? EmailErrorCodes.SmtpPermanentFailure,
                cancellationToken),
            _ => store.ScheduleRetryAsync(
                claimed.Id,
                leaseId,
                RetryDelay.Seconds(claimed.AttemptCount, options.RetryBaseDelaySeconds, options.RetryMaxDelaySeconds),
                result.ErrorCode ?? EmailErrorCodes.SmtpTemporaryFailure,
                cancellationToken)
        };
}

internal static class RetryDelay
{
    /// <summary>Capped exponential backoff with jitter (50–100% of the capped delay).</summary>
    public static int Seconds(int attempt, int baseDelaySeconds, int maxDelaySeconds)
    {
        double exponential = baseDelaySeconds * Math.Pow(2, Math.Max(0, attempt - 1));
        double capped = Math.Min(exponential, maxDelaySeconds);

#pragma warning disable CA5394 // Jitter does not need a cryptographically secure generator.
        double jitter = 0.5 + Random.Shared.NextDouble() * 0.5;
#pragma warning restore CA5394

        return Math.Max(1, (int)Math.Round(capped * jitter));
    }
}
