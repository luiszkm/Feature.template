using App.Host.Seeders;
using App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Host;

public static class DatabaseHostExtensions
{
    public static async Task MigrateAndSeedAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Database.IsInMemory())
            await db.Database.MigrateAsync(cancellationToken);

        await AppSeeder.SeedAsync(app.Services, cancellationToken);
    }
}
