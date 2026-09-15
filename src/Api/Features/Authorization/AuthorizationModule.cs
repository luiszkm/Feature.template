using Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Features.Authorization;

public static class AuthorizationModule
{
    public static IServiceCollection AddAuthorizationModule(this IServiceCollection services)
    {
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserAssignmentRepository, UserAssignmentRepository>();
        services.AddScoped<IUserRolesProvider, UserRolesProvider>();
        services.AddSingleton<ITenantQueryFilterConfigurator, AuthorizationTenantQueryFilters>();
        return services;
    }

    public static AuthorizationOptions AddAuthorizationModulePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(SecurityPolicies.AuthorizationRolesRead, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    AuthorizationPermissions.RoleRead)));

        options.AddPolicy(SecurityPolicies.AuthorizationRolesManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    AuthorizationPermissions.RoleManage)));

        options.AddPolicy(SecurityPolicies.AuthorizationPermissionsRead, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    AuthorizationPermissions.PermissionRead)));

        options.AddPolicy(SecurityPolicies.AuthorizationPermissionsManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    AuthorizationPermissions.PermissionManage)));

        return options;
    }
}

internal sealed class AuthorizationTenantQueryFilters : ITenantQueryFilterConfigurator
{
    public void Configure(ModelBuilder modelBuilder, AppDbContext dbContext)
    {
        modelBuilder.Entity<Role>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<Permission>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<RolePermission>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<UserAssignment>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);
    }
}
