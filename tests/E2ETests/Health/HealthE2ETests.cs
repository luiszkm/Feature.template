using System.Net;
using E2ETests.Common;

namespace E2ETests.Health;

public sealed class HealthE2ETests
{
    [Fact]
    public async Task HealthLive_ShouldReturn200()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ShouldReturn200_WhenDatabaseIsInMemory()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
