using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using MyCollection.Results;

namespace MyCollection.ServiceDefaults;

/// <summary>
/// The single mapping from <see cref="ErrorKind"/> to an HTTP status and an RFC 9457
/// ProblemDetails body carrying the stable <c>code</c> and a <c>traceId</c> (AD-13).
/// </summary>
public static class ErrorResults
{
    public static IResult ToProblem(this Error error, HttpContext httpContext) =>
        HttpResults.Problem(
            title: error.Message,
            statusCode: StatusCodeFor(error.Kind),
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier
            });

    public static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped error kind.")
    };
}
