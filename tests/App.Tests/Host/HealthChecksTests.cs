using System.Net;
using App.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Tests.Host;

public sealed class HealthChecksTests
{
    [Fact]
    public async Task HealthLive_ShouldReturn200()
    {
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ShouldReturn200_WhenDatabaseIsAvailable()
    {
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
