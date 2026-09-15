using Api.Features.Tenants;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Host.Seeders;

internal static class AgentSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provisioner = scope.ServiceProvider.GetRequiredService<IDefaultAgentProvisioner>();

        var tenantIds = await db.Set<Tenant>()
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
            await provisioner.EnsureDefaultAgentAsync(tenantId, cancellationToken);
    }
}
