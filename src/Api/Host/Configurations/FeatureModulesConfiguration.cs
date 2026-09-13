using Api.Features.Authorization;
using Api.Features.Ai;
using Api.Features.Identity;
using Api.Features.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Host.Configurations;

public static class FeatureModulesConfiguration
{
    public static IServiceCollection AddFeatureModules(this IServiceCollection services) =>
        services
            .AddIdentityModule()
            .AddAuthorizationModule()
            .AddTenantsModule()
            .AddAiModule();
}
