using Api.Host.Configurations;
using Api.Host.Extensions;
using Api.Host.Middleware;
using Serilog;

namespace Api.Host;

public static class HostApplicationExtensions
{
    public static WebApplicationBuilder AddHostLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Product.Template.v2")
            .CreateLogger();

        builder.Host.UseSerilog();
        return builder;
    }

    public static WebApplication UseHostApplication(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseSerilogRequestLogging();
        app.UseExceptionHandling();
        app.UseCors(SecurityConfiguration.DefaultCorsPolicyName);
        app.UseRateLimiter();
        app.UseHostPipeline();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHealthChecksHost();
        app.MapOpenApiHost();
        app.MapEndpointsFromAssembly();
        return app;
    }
}
