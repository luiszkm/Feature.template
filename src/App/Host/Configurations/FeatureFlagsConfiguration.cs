using Microsoft.FeatureManagement;

namespace App.Host.Configurations;

public static class FeatureFlagsConfiguration
{
    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddFeatureManagement(configuration.GetSection("FeatureFlags"));
        return services;
    }
}
