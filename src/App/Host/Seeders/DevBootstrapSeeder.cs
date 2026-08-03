using App.Features.Authorization;
using App.Features.Identity;
using App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Host.Seeders;

internal static class DevBootstrapSeeder
{
    public static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AdminRoleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-cccc-aaaaaaaaaaaa");

    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var tenantContext = sp.GetRequiredService<ITenantContext>();
        if (tenantContext is TenantContext mutable)
            mutable.SetTenant(WellKnownTenants.Development, "dev");

        var db = sp.GetRequiredService<AppDbContext>();
        var configuration = sp.GetRequiredService<IConfiguration>();
        var environment = sp.GetRequiredService<IHostEnvironment>();
        var passwordHasher = sp.GetRequiredService<IPasswordHasher>();
        var seedOptions = configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();

        await SeedPermissionsAsync(db, WellKnownTenants.Development, cancellationToken);

        if (await db.Set<Role>().IgnoreQueryFilters()
                .AnyAsync(r => r.Id == AdminRoleId, cancellationToken))
            return;

        var permissions = await db.Set<Permission>()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == WellKnownTenants.Development)
            .ToListAsync(cancellationToken);

        var adminRole = Role.CreateWithId(
            AdminRoleId,
            WellKnownTenants.Development,
            "Admin",
            "Full system access");

        foreach (var permission in permissions)
            adminRole.AssignPermission(permission.Id);

        await db.Set<Role>().AddAsync(adminRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await SeedAdminUserAsync(
            db,
            passwordHasher,
            seedOptions,
            environment,
            cancellationToken);

        var assignment = UserAssignment.Create(
            AdminUserId,
            AdminRoleId,
            WellKnownTenants.Development);

        await db.Set<UserAssignment>().AddAsync(assignment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPermissionsAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingNames = (await db.Set<Permission>()
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = PermissionCatalog.All
            .Where(entry => !existingNames.Contains(entry.Name))
            .Select(entry => Permission.Create(tenantId, entry.Name, entry.Description))
            .ToList();

        if (toAdd.Count == 0)
            return;

        await db.Set<Permission>().AddRangeAsync(toAdd, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        SeedOptions seedOptions,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (await db.Set<User>().IgnoreQueryFilters()
                .AnyAsync(u => u.Id == AdminUserId, cancellationToken))
            return;

        var password = seedOptions.AdminPassword
            ?? (environment.IsDevelopment() ? SeedOptions.DefaultDevPassword : null);

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Seed:AdminPassword must be configured outside Development.");

        var admin = User.CreateWithId(
            AdminUserId,
            WellKnownTenants.Development,
            Email.Create(seedOptions.AdminEmail),
            passwordHasher.Hash(password),
            seedOptions.AdminFirstName,
            seedOptions.AdminLastName);

        admin.ConfirmEmail();

        await db.Set<User>().AddAsync(admin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
