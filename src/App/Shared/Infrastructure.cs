using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Shared;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string? ConnectionString { get; init; }

    public bool UseInMemory { get; init; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext,
    IEnumerable<ITenantQueryFilterConfigurator> tenantFilterConfigurators) : DbContext(options), IUnitOfWork
{
    public Guid? CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var configurator in tenantFilterConfigurators)
            configurator.Configure(modelBuilder, this);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        var connectionString = configuration.GetConnectionString("Default")
            ?? databaseOptions.ConnectionString;

        var useInMemory = databaseOptions.UseInMemory;

        if (!useInMemory && string.IsNullOrWhiteSpace(connectionString))
            useInMemory = true;

        services.AddDbContext<AppDbContext>((_, options) =>
        {
            if (useInMemory)
            {
                options.UseInMemoryDatabase("AppDb");
                return;
            }

            options.UseNpgsql(connectionString);

            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }
}
