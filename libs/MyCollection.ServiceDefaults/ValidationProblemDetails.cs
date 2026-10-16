using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MyCollection.ServiceDefaults;

/// <summary>
/// Makes every ProblemDetails the framework produces carry the same <c>code</c>/<c>traceId</c>
/// shape as every other error path (AD-13): the automatic Minimal API validation failure (from
/// <c>AddValidation()</c>) and the bare 400/415 the framework answers when it rejects a body
/// before validation can run (unparseable JSON, empty body, wrong content type). Bodies from
/// <see cref="ErrorResults.ToProblem"/> already carry both and are left untouched.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string ValidationFailedCode = "validation_failed";

    public const string MalformedRequestCode = "malformed_request";

    public const string UnsupportedMediaTypeCode = "unsupported_media_type";

    public static IServiceCollection AddMyCollectionProblemDetails(this IServiceCollection services)
    {
        // Minimal APIs throw on a rejected body in Development and only set the status code
        // elsewhere. Never throwing keeps one path in every environment: the bare 400/415 that
        // UseMyCollectionProblemDetails turns into ProblemDetails.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

        return services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                var status = problem.Status ?? context.HttpContext.Response.StatusCode;

                var code = problem switch
                {
                    HttpValidationProblemDetails => ValidationFailedCode,
                    _ when status == StatusCodes.Status400BadRequest => MalformedRequestCode,
                    _ when status == StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaTypeCode,
                    _ => null
                };

                // TryAdd: a body that already names its code (ErrorResults.ToProblem) keeps it.
                if (code is not null)
                {
                    problem.Extensions.TryAdd("code", code);
                }

                problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
            };
        });
    }

    /// <summary>
    /// Turns any error status the framework sets without a body (e.g. a rejected request body)
    /// into ProblemDetails via the pipeline configured by <see cref="AddMyCollectionProblemDetails"/>.
    /// </summary>
    public static WebApplication UseMyCollectionProblemDetails(this WebApplication app)
    {
        app.UseStatusCodePages();
        return app;
    }
}
