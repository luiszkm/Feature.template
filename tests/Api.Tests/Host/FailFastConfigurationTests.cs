using Api.Host.Configurations;
using Api.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Tests.Host;

public sealed class FailFastConfigurationTests
{
    [Fact]
    public void AddInfrastructure_ShouldThrow_WhenConnectionStringMissingAndInMemoryDisabled()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:UseInMemory"] = "false",
            ["ConnectionStrings:Default"] = ""
        });
        var environment = new StubHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration, environment));

        Assert.Contains("connection string is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddInfrastructure_ShouldAllowInMemory_WhenExplicitlyEnabled()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:UseInMemory"] = "true",
            ["ConnectionStrings:Default"] = ""
        });
        var environment = new StubHostEnvironment(Environments.Production);

        var exception = Record.Exception(
            () => services.AddInfrastructure(configuration, environment));

        Assert.Null(exception);
    }

    [Fact]
    public void AddSecurity_ShouldThrow_WhenJwtSecretMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Enabled"] = "true",
            ["Jwt:Secret"] = "",
            ["Cors:AllowedOrigins:0"] = "http://localhost:5080"
        });
        var environment = new StubHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddSecurity(configuration, environment));

        Assert.Contains("Jwt:Secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSecurity_ShouldThrow_WhenCorsWildcardOutsideDev()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Enabled"] = "false",
            ["Cors:AllowedOrigins:0"] = "*"
        });
        var environment = new StubHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddSecurity(configuration, environment));

        Assert.Contains("Cors:AllowedOrigins", ex.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
