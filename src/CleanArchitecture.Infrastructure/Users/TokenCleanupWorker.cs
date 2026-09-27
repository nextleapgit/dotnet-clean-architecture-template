using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Users;

/// <summary>Periodically deletes ended tokens in batches, each batch in a fresh scope.</summary>
internal sealed class TokenCleanupWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<TokenCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<TokenCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TokenCleanupOptions settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Token cleanup is disabled; ended tokens are kept");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunPassAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
#pragma warning disable S6667
                logger.LogError("Token cleanup pass failed with {ExceptionType}", exception.GetType().Name);
#pragma warning restore S6667
            }

            await Task.Delay(TimeSpan.FromMinutes(settings.IntervalMinutes), timeProvider, stoppingToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RunPassAsync(TokenCleanupOptions settings, CancellationToken cancellationToken)
    {
        int refreshTokens = await DeleteInBatchesAsync(
            (store, token) => store.DeleteEndedRefreshTokenFamiliesAsync(settings.BatchSize, settings.RetentionDays, token),
            cancellationToken);

        int userTokens = await DeleteInBatchesAsync(
            (store, token) => store.DeleteEndedUserTokensAsync(settings.BatchSize, settings.RetentionDays, token),
            cancellationToken);

        if (refreshTokens > 0 || userTokens > 0)
        {
            logger.LogInformation(
                "Token cleanup deleted {RefreshTokens} refresh tokens and {UserTokens} emailed tokens",
                refreshTokens,
                userTokens);
        }
    }

    // Repeats until a batch deletes nothing, so a backlog is cleared in one pass without one huge statement.
    private async Task<int> DeleteInBatchesAsync(
        Func<TokenCleanupStore, CancellationToken, Task<int>> deleteBatch,
        CancellationToken cancellationToken)
    {
        int total = 0;
        int deleted;

        do
        {
            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            deleted = await deleteBatch(scope.ServiceProvider.GetRequiredService<TokenCleanupStore>(), cancellationToken);
            total += deleted;
        }
        while (deleted > 0 && !cancellationToken.IsCancellationRequested);

        return total;
    }
}
