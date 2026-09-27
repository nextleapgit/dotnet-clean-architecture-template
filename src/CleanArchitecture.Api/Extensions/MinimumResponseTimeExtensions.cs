namespace CleanArchitecture.Api.Extensions;

internal static class MinimumResponseTimeExtensions
{
    /// <summary>
    /// Delays the response until at least <paramref name="minimum"/> has passed, so its timing does not
    /// reveal which path the request took (e.g. whether an email is registered). Choose a minimum above
    /// the slowest normal path; a slower request still stands out.
    /// </summary>
    public static RouteHandlerBuilder WithMinimumResponseTime(this RouteHandlerBuilder builder, TimeSpan minimum) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            TimeProvider timeProvider = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            long startedAt = timeProvider.GetTimestamp();

            object? result = await next(context);

            TimeSpan remaining = minimum - timeProvider.GetElapsedTime(startedAt);

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, timeProvider, context.HttpContext.RequestAborted)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            return result;
        });
}
