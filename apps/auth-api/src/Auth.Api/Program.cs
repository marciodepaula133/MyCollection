using Auth.Api.Endpoints;
using Auth.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyCollection.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAuthInfrastructure();
builder.Services.AddValidation();
builder.Services.AddMyCollectionProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Live: the process is up. Ready: the database is reachable and fully migrated (AD-12).
// AD-19 compose gates gateway startup on ready.
app.MapGet("/health/live", () => Results.Ok());

// A MapGet over HealthCheckService rather than MapHealthChecks: the OpenAPI generator only
// describes handler-backed endpoints, and MapHealthChecks has none.
app.MapGet("/health/ready", async (HealthCheckService health, CancellationToken cancellationToken) =>
    {
        var report = await health.CheckHealthAsync(cancellationToken);
        return Results.Text(
            report.Status.ToString(),
            statusCode: report.Status == HealthStatus.Healthy
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

app.MapGroup("/api/auth").MapRegister();

app.Run();

// Exposed for WebApplicationFactory<Program> in Auth.IntegrationTests.
public partial class Program { }
