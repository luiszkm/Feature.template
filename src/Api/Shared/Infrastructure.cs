using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Shared;

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

/// <summary>
/// Design-time factory so `dotnet ef` can build the model without booting the web host
/// (JWT fail-fast, seed, or a live PostgreSQL). Query filters stay runtime-only via
/// <see cref="ITenantQueryFilterConfigurator"/>, matching the existing snapshot.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=product_template;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options, new TenantContext(), []);
    }
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

        // InMemory is allowed only when explicitly opted in, or under the Testing environment.
        var useInMemory = databaseOptions.UseInMemory || environment.IsEnvironment("Testing");

        if (!useInMemory && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A database connection string is required when Database:UseInMemory is false. " +
                "Set ConnectionStrings:Default (or Database:ConnectionString), or set Database:UseInMemory=true.");
        }

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
