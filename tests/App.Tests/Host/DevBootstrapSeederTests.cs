using App.Features.Authorization;
using App.Features.Identity;
using App.Features.Tenants;
using App.Host.Seeders;
using App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace App.Tests.Host;

public sealed class DevBootstrapSeederTests
{
    [Fact]
    public async Task SeedAsync_ShouldAssignMissingPermissions_WhenAdminRoleAlreadyExists()
    {
        await using var provider = CreateSeedProvider(
            nameof(SeedAsync_ShouldAssignMissingPermissions_WhenAdminRoleAlreadyExists));

        await DevBootstrapSeeder.SeedAsync(provider);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.Set<RolePermission>()
                .IgnoreQueryFilters()
                .FirstAsync(rp => rp.RoleId == DevBootstrapSeeder.AdminRoleId);

            db.Set<RolePermission>().Remove(assignment);
            await db.SaveChangesAsync();
        }

        await DevBootstrapSeeder.SeedAsync(provider);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var permissionCount = await db.Set<Permission>()
                .IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == WellKnownTenants.Development);
            var assignedCount = await db.Set<RolePermission>()
                .IgnoreQueryFilters()
                .CountAsync(rp => rp.RoleId == DevBootstrapSeeder.AdminRoleId);

            Assert.True(permissionCount > 0);
            Assert.Equal(permissionCount, assignedCount);
        }
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateAdminUser_WhenRoleExistsButUserMissing()
    {
        await using var provider = CreateSeedProvider(
            nameof(SeedAsync_ShouldCreateAdminUser_WhenRoleExistsButUserMissing));

        await DevBootstrapSeeder.SeedAsync(provider);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Set<User>()
                .IgnoreQueryFilters()
                .FirstAsync(u => u.Id == DevBootstrapSeeder.AdminUserId);
            db.Set<User>().Remove(admin);

            var assignments = await db.Set<UserAssignment>()
                .IgnoreQueryFilters()
                .Where(a => a.UserId == DevBootstrapSeeder.AdminUserId)
                .ToListAsync();
            db.Set<UserAssignment>().RemoveRange(assignments);
            await db.SaveChangesAsync();
        }

        await DevBootstrapSeeder.SeedAsync(provider);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Set<User>()
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Id == DevBootstrapSeeder.AdminUserId));
            Assert.True(await db.Set<UserAssignment>()
                .IgnoreQueryFilters()
                .AnyAsync(a =>
                    a.UserId == DevBootstrapSeeder.AdminUserId
                    && a.RoleId == DevBootstrapSeeder.AdminRoleId));
        }
    }

    private static ServiceProvider CreateSeedProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddIdentityModule();
        services.AddAuthorizationModule();
        services.AddTenantsModule();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IHostEnvironment>(_ => new SeedTestHostEnvironment());
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:AdminPassword"] = "TestPassword1!",
                    ["Seed:AdminEmail"] = "admin@producttemplate.com"
                })
                .Build());

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<Tenant>().Add(
            Tenant.CreateWithId(
                WellKnownTenants.Development,
                "dev",
                "Development Tenant",
                null,
                TenantIsolationMode.SharedDb));
        db.SaveChanges();

        return provider;
    }

    private sealed class SeedTestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "App.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
