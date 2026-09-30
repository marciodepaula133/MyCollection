using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Auth.IntegrationTests;

/// <summary>
/// Empty skeleton for this setup slice (AD-20). Exercises only the unconditional health
/// stubs added in this setup - no database, no Testcontainers - so it's not yet a real
/// integration test. Replaced/expanded starting with CAP-1, once there's a DbContext to
/// spin up against a real PostgreSQL 18 via Testcontainers.
/// </summary>
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoint_reports_healthy(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
