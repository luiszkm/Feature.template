using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace E2ETests.Common;

public static class E2EWebApplicationFactory
{
    public static WebApplicationFactory<Program> Create(
        Action<Dictionary<string, string?>>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = string.Empty,
            ["Database:UseInMemory"] = "true",
            ["Seed:AdminPassword"] = "TestPassword1!",
            ["Jwt:Secret"] = "test-secret-key-minimum-32-characters-long",
            ["Jwt:Enabled"] = "true",
            ["FeatureFlags:EnableAI"] = "true"
        };

        configure?.Invoke(settings);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(settings);
            });
        });
    }
}
