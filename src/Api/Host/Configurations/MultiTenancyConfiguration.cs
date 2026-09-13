using App.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Host.Configurations;

public static class MultiTenancyConfiguration
{
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MultiTenancyOptions>(configuration.GetSection(MultiTenancyOptions.SectionName));
        services.AddSingleton<ITenantResolver, SubdomainThenHeaderTenantResolver>();
        return services;
    }
}
