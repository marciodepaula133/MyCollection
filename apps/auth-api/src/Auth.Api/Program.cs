using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Minimal, unconditionally-healthy stubs (no DbContext exists yet, so there is no
// pending-migrations or database check to run). AD-19 compose gates gateway startup
// on this reporting healthy; a real check is added alongside CAP-1.
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());

app.Run();

// Exposed for WebApplicationFactory<Program> in Auth.IntegrationTests.
public partial class Program { }
