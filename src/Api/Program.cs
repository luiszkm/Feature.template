using App.Host;
using App.Host.Configurations;
using App.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddHostLogging();
builder.Services
    .AddPlatform()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddMultiTenancy(builder.Configuration)
    .AddFeatureModules()
    .AddSecurity(builder.Configuration, builder.Environment)
    .AddFeatureFlags(builder.Configuration)
    .AddObservability(builder.Configuration)
    .AddHealthChecksHost()
    .AddEndpoints()
    .AddOpenApi();

var app = builder.Build();

await app.MigrateAndSeedAsync();
app.UseHostApplication();
app.Run();

public partial class Program;
