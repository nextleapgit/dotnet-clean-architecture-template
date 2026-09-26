using CleanArchitecture.BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchitecture.Api.Common;

/// <summary>Unexpected exceptions become problem details without leaking internal details.</summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails = exception switch
        {
            ConcurrencyConflictException => Conflict(
                "General.ConcurrencyConflict",
                "The data was modified by another request. Retry the operation."),
            UniqueConstraintViolationException => Conflict(
                "General.UniqueConstraintViolation",
                "The data conflicts with a record created by another request."),
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "Server failure",
                Extensions = { ["code"] = "General.ServerFailure" }
            }
        };

        if (problemDetails.Status == StatusCodes.Status409Conflict)
        {
            logger.LogWarning(exception, "Request conflicted with concurrent data changes");
        }
        else
        {
            logger.LogError(exception, "Unhandled exception occurred");
        }

        problemDetails.Extensions["correlationId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails Conflict(string code, string detail) =>
        new()
        {
            Status = StatusCodes.Status409Conflict,
            Type = ConflictType,
            Title = code,
            Detail = detail,
            Extensions = { ["code"] = code }
        };
}
