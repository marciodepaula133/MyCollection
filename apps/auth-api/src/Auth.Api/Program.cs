using Auth.Api.Endpoints;
using Auth.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAuthInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Live: the process is up. Ready: the database is reachable and fully migrated (AD-12).
// AD-19 compose gates gateway startup on ready.
app.MapGet("/health/live", () => Results.Ok());
app.MapHealthChecks("/health/ready");

app.MapGroup("/api/auth").MapRegister();

app.Run();

// Exposed for WebApplicationFactory<Program> in Auth.IntegrationTests.
public partial class Program { }
