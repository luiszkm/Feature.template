using Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Features.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserLookup, UserLookup>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISecurityStampService, SecurityStampService>();
        services.AddMemoryCache();
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

internal sealed class UserLookup(IUserRepository users) : IUserLookup
{
    public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await users.GetByIdAsync(userId, cancellationToken) is not null;
}

internal sealed class UserDirectory(IUserRepository users) : IUserDirectory
{
    public async Task<PaginatedListOutput<UserDirectoryEntry>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await users.ListAllAsync(query, cancellationToken);
        return new PaginatedListOutput<UserDirectoryEntry>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(user => new UserDirectoryEntry(
                user.Id,
                user.Email.Value,
                user.FirstName,
                user.LastName)).ToList());
    }
}
