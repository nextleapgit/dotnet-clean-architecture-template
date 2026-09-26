using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Runs sequential passes — cleanup, settlement, dispatch one message — each in a fresh scope.
/// Messages are sent one at a time per instance; instances coordinate through row locks.
/// </summary>
internal sealed class EmailOutboxWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<EmailOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private DateTimeOffset _nextCleanupAt = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EmailOutboxOptions settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Email outbox worker is disabled; queued emails are not sent");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            bool dispatched = false;

            try
            {
                dispatched = await RunPassAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Only the exception type is logged; its message may contain private data.
#pragma warning disable S6667
                logger.LogError("Email outbox pass failed with {ExceptionType}", exception.GetType().Name);
#pragma warning restore S6667
            }

            if (!dispatched)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), timeProvider, stoppingToken)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private async Task<bool> RunPassAsync(EmailOutboxOptions settings, CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() >= _nextCleanupAt)
        {
            await using AsyncServiceScope cleanupScope = serviceScopeFactory.CreateAsyncScope();
            await cleanupScope.ServiceProvider.GetRequiredService<EmailOutboxStore>()
                .DeleteTerminalAsync(settings.CleanupBatchSize, settings.RetentionDays, cancellationToken);

            _nextCleanupAt = timeProvider.GetUtcNow().AddMinutes(settings.CleanupIntervalMinutes);
        }

        await using (AsyncServiceScope settlementScope = serviceScopeFactory.CreateAsyncScope())
        {
            await settlementScope.ServiceProvider.GetRequiredService<EmailOutboxStore>()
                .SettleAsync(settings.SettlementBatchSize, settings.MaxAttempts, cancellationToken);
        }

        await using AsyncServiceScope dispatchScope = serviceScopeFactory.CreateAsyncScope();

        return await dispatchScope.ServiceProvider.GetRequiredService<EmailOutboxDispatcher>()
            .DispatchOneAsync(cancellationToken);
    }
}
