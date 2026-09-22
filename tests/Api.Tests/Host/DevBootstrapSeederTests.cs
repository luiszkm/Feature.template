using Api.Features.Authorization;
using Api.Features.Identity;
using Api.Features.Tenants;
using Api.Host.Seeders;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Tests.Host;

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
        const string adminEmail = "admin@producttemplate.com";
        await using var provider = CreateSeedProvider(
            nameof(SeedAsync_ShouldCreateAdminUser_WhenRoleExistsButUserMissing));

        await DevBootstrapSeeder.SeedAsync(provider);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Set<User>()
                .IgnoreQueryFilters()
                .FirstAsync(u => u.Email.Value == adminEmail);
            db.Set<User>().Remove(admin);

            var assignments = await db.Set<UserAssignment>()
                .IgnoreQueryFilters()
                .Where(a => a.UserId == admin.Id)
                .ToListAsync();
            db.Set<UserAssignment>().RemoveRange(assignments);
            await db.SaveChangesAsync();
        }

        await DevBootstrapSeeder.SeedAsync(provider);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Set<User>()
                .IgnoreQueryFilters()
                .SingleAsync(u => u.Email.Value == adminEmail);
            Assert.True(await db.Set<UserAssignment>()
                .IgnoreQueryFilters()
                .AnyAsync(a =>
                    a.UserId == admin.Id
                    && a.RoleId == DevBootstrapSeeder.AdminRoleId));
        }
    }

    /// <summary>Own store root, so "restart" providers of one test still see each other's rows.</summary>
    private static readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot SeedDatabaseRoot = new();

    private static ServiceProvider CreateSeedProvider(string databaseName)
    {
        var services = new ServiceCollection();
        // This container registers only some modules' tenant filters. EF caches the model per
        // internal service provider, which every InMemory context in the process shares - so
        // without its own provider this test could build the model first and strip the filters
        // of the modules it leaves out (Ai) from every other test in the run.
        services.AddDbContext<AppDbContext>(options => options
            .UseInMemoryDatabase(databaseName, SeedDatabaseRoot)
            .EnableServiceProviderCaching(false));
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
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
