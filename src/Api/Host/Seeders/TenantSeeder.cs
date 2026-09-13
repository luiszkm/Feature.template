using Api.Features.Tenants;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Host.Seeders;

internal static class TenantSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await EnsureTenantAsync(
            db,
            WellKnownTenants.Public,
            "public",
            "Public Tenant",
            cancellationToken);

        await EnsureTenantAsync(
            db,
            WellKnownTenants.Development,
            "dev",
            "Development Tenant",
            cancellationToken);
    }

    private static async Task EnsureTenantAsync(
        AppDbContext db,
        Guid tenantId,
        string tenantKey,
        string displayName,
        CancellationToken cancellationToken)
    {
        var exists = await db.Set<Tenant>().AnyAsync(
            t => t.Id == tenantId || t.TenantKey == tenantKey,
            cancellationToken);

        if (exists)
            return;

        await db.Set<Tenant>().AddAsync(
            Tenant.CreateWithId(
                tenantId,
                tenantKey,
                displayName,
                contactEmail: null,
                TenantIsolationMode.SharedDb),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
