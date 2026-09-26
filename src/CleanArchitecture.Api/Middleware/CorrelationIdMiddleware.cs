using System.Buffers;
using Serilog.Context;

namespace CleanArchitecture.Api.Middleware;

/// <summary>
/// Accepts a safe client-supplied Correlation-Id or falls back to the trace identifier, then uses it as
/// the request's TraceIdentifier so logs, audit entries, and error responses all carry the same value.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Correlation-Id";

    private const int MaxLength = 64;

    private static readonly SearchValues<char> AllowedCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.:");

    public async Task InvokeAsync(HttpContext context)
    {
        string? requested = context.Request.Headers[HeaderName].FirstOrDefault();

        // Untrusted input ends up in logs and audit records: accept only short, plain identifiers.
        if (IsSafe(requested))
        {
            context.TraceIdentifier = requested!;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
        {
            await next(context);
        }
    }

    private static bool IsSafe(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length <= MaxLength &&
        !value.AsSpan().ContainsAnyExcept(AllowedCharacters);
}
