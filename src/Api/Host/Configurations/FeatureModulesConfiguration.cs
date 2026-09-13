using App.Features.Authorization;
using App.Features.Ai;
using App.Features.Identity;
using App.Features.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace App.Host.Configurations;

public static class FeatureModulesConfiguration
{
    public static IServiceCollection AddFeatureModules(this IServiceCollection services) =>
        services
            .AddIdentityModule()
            .AddAuthorizationModule()
            .AddTenantsModule()
            .AddAiModule();
}
