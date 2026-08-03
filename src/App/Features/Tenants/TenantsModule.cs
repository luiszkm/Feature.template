using App.Host.Security;
using App.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace App.Features.Tenants;

public static class TenantsModule
{
    public static IServiceCollection AddTenantsModule(this IServiceCollection services)
    {
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantStore, TenantStore>();
        return services;
    }

    public static AuthorizationOptions AddTenantsModulePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(SecurityPolicies.TenantsRead, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    TenantsPermissions.Read)));

        options.AddPolicy(SecurityPolicies.TenantsManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    TenantsPermissions.Manage)));

        return options;
    }
}
