using Microsoft.Extensions.DependencyInjection;

namespace App.Host.Seeders;

public static class AppSeeder
{
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await SeedLock.WaitAsync(cancellationToken);
        try
        {
            await TenantSeeder.SeedAsync(services, cancellationToken);
            await DevBootstrapSeeder.SeedAsync(services, cancellationToken);
        }
        finally
        {
            SeedLock.Release();
        }
    }
}
