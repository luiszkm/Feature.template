using App.Host.Security;
using App.Host.Security.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Host.Configurations;

public static class AuthenticationProvidersConfiguration
{
    public static IServiceCollection AddAuthenticationProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue("MicrosoftAuth:Enabled", false))
        {
            services.AddOptions<MicrosoftAuthSettings>()
                .Bind(configuration.GetSection("MicrosoftAuth"))
                .ValidateOnStart();

            services.AddHttpClient<MicrosoftAuthenticationProvider>();
            services.AddScoped<IAuthenticationProvider, MicrosoftAuthenticationProvider>();
        }

        services.AddScoped<IAuthenticationProviderFactory, AuthenticationProviderFactory>();
        return services;
    }
}
