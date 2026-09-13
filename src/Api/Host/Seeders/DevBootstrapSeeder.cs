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
        await EnsureAdminRoleWithPermissionsAsync(db, WellKnownTenants.Development, cancellationToken);
        await SeedAdminUserAsync(db, passwordHasher, seedOptions, environment, cancellationToken);
        await EnsureAdminAssignmentAsync(db, seedOptions, cancellationToken);
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

    private static async Task EnsureAdminRoleWithPermissionsAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var adminRoleExists = await db.Set<Role>()
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Id == AdminRoleId, cancellationToken);

        if (!adminRoleExists)
        {
            var adminRole = Role.CreateWithId(
                AdminRoleId,
                tenantId,
                "Admin",
                "Full system access");

            await db.Set<Role>().AddAsync(adminRole, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var permissions = await db.Set<Permission>()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var assignedIds = (await db.Set<RolePermission>()
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == AdminRoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var missingIds = permissions.Where(id => !assignedIds.Contains(id)).ToList();
        if (missingIds.Count == 0)
            return;

        foreach (var permissionId in missingIds)
        {
            await db.Set<RolePermission>().AddAsync(
                RolePermission.Create(AdminRoleId, permissionId, tenantId),
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        SeedOptions seedOptions,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var adminEmail = Email.Create(seedOptions.AdminEmail);
        if (await db.Set<User>().IgnoreQueryFilters()
                .AnyAsync(u => u.Email.Value == adminEmail.Value, cancellationToken))
            return;

        var password = seedOptions.AdminPassword
            ?? (environment.IsDevelopment() ? SeedOptions.DefaultDevPassword : null);

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Seed:AdminPassword must be configured outside Development.");

        var admin = User.Create(
            WellKnownTenants.Development,
            adminEmail,
            passwordHasher.Hash(password),
            seedOptions.AdminFirstName,
            seedOptions.AdminLastName);

        await db.Set<User>().AddAsync(admin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdminAssignmentAsync(
        AppDbContext db,
        SeedOptions seedOptions,
        CancellationToken cancellationToken)
    {
        var adminEmail = Email.Create(seedOptions.AdminEmail).Value;
        var admin = await db.Set<User>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.Value == adminEmail, cancellationToken);

        if (admin is null)
            throw new InvalidOperationException(
                $"Seed admin user '{adminEmail}' was not found after SeedAdminUserAsync.");

        var exists = await db.Set<UserAssignment>()
            .IgnoreQueryFilters()
            .AnyAsync(
                a => a.UserId == admin.Id && a.RoleId == AdminRoleId,
                cancellationToken);

        if (exists)
            return;

        var assignment = UserAssignment.Create(
            admin.Id,
            AdminRoleId,
            WellKnownTenants.Development);

        await db.Set<UserAssignment>().AddAsync(assignment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
