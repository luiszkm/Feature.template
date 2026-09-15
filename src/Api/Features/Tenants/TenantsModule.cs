using Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Features.Tenants;

public static class TenantsModule
{
    public static IServiceCollection AddTenantsModule(this IServiceCollection services)
    {
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantDirectory, TenantDirectory>();
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

internal sealed class TenantDirectory(ITenantRepository tenants) : ITenantDirectory
{
    public async Task<PaginatedListOutput<TenantDirectoryEntry>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await tenants.ListAllAsync(query, cancellationToken);
        return new PaginatedListOutput<TenantDirectoryEntry>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(tenant => new TenantDirectoryEntry(
                tenant.Id,
                tenant.TenantKey,
                tenant.IsolationMode,
                tenant.IsActive)).ToList());
    }
}
