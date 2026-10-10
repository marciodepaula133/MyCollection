using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MyCollection.ServiceDefaults;

/// <summary>
/// Makes the framework's automatic Minimal API validation failure (from <c>AddValidation()</c>)
/// carry the same <c>code</c>/<c>traceId</c> shape as every other error path (AD-13), without
/// touching <see cref="ErrorResults.ToProblem"/>'s plain-<c>ProblemDetails</c> path.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddMyCollectionProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (context.ProblemDetails is not HttpValidationProblemDetails)
                {
                    return;
                }

                context.ProblemDetails.Extensions.TryAdd("code", "validation_failed");
                context.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
            };
        });
}
