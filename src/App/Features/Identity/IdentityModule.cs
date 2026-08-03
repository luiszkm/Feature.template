using App.Host.Security;
using App.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Features.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISecurityStampService, SecurityStampService>();
        services.AddMemoryCache();
        services.AddSingleton<IEmailConfirmationTokenService, EmailConfirmationTokenService>();
        services.AddSingleton<IAuthorizationHandler, SelfOrPermissionHandler>();
        services.AddSingleton<ITenantQueryFilterConfigurator, IdentityTenantQueryFilters>();
        return services;
    }

    public static AuthorizationOptions AddIdentityModulePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(SecurityPolicies.UsersRead, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    IdentityPermissions.UserRead)));

        options.AddPolicy(SecurityPolicies.UsersManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(
                    AuthorizationClaimTypes.Permission,
                    IdentityPermissions.UserManage)));

        options.AddPolicy(SecurityPolicies.UserReadOrSelf, policy =>
            policy.Requirements.Add(new SelfOrPermissionRequirement(IdentityPermissions.UserRead)));

        options.AddPolicy(SecurityPolicies.UserManageOrSelf, policy =>
            policy.Requirements.Add(new SelfOrPermissionRequirement(IdentityPermissions.UserManage)));

        return options;
    }
}

internal sealed class IdentityTenantQueryFilters : ITenantQueryFilterConfigurator
{
    public void Configure(ModelBuilder modelBuilder, AppDbContext dbContext)
    {
        modelBuilder.Entity<User>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<RefreshToken>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);
    }
}
