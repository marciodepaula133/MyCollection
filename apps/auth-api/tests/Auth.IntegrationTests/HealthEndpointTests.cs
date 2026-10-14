using System.Net;
using Xunit;

namespace Auth.IntegrationTests;

public class HealthEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    [Fact]
    public async Task Live_is_healthy_without_touching_the_database()
    {
        var response = await fixture.Unmigrated.CreateClient()
            .GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_is_healthy_once_migrations_are_applied()
    {
        var response = await fixture.Migrated.CreateClient()
            .GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_reports_unhealthy_while_migrations_are_pending()
    {
        var response = await fixture.Unmigrated.CreateClient()
            .GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
