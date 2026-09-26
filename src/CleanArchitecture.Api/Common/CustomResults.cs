using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Common;

/// <summary>
/// Maps failed results to RFC 9457 problem details. Every body carries the stable, localizable
/// <c>code</c> and the request's <c>correlationId</c>; <c>detail</c> holds the message.
/// </summary>
public static class CustomResults
{
    public static IResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted to a problem.");
        }

        Error error = result.Error;
        bool isExpected = error.Type != ErrorType.Failure;

        Dictionary<string, object?> extensions = new()
        {
            ["code"] = isExpected ? error.Code : "General.ServerFailure"
        };

        if (error is ValidationError validationError)
        {
            extensions["errors"] = validationError.Errors;
        }

        return new CorrelatedProblemResult(Results.Problem(
            title: isExpected ? error.Code : "Server failure",
            detail: isExpected ? error.Description : "An unexpected error occurred",
            type: GetType(error.Type),
            statusCode: GetStatusCode(error.Type),
            extensions: extensions));
    }

    public static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation or ErrorType.Problem => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetType(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation or ErrorType.Problem => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            ErrorType.Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            ErrorType.NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            ErrorType.Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        };

    /// <summary>Adds the correlation id at execution time, when the HttpContext is known.</summary>
    private sealed class CorrelatedProblemResult(IResult inner) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            if (inner is Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem)
            {
                problem.ProblemDetails.Extensions["correlationId"] = httpContext.TraceIdentifier;
            }

            return inner.ExecuteAsync(httpContext);
        }
    }
}
