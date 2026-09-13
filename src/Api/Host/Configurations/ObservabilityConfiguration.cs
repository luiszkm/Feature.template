using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Host.Configurations;

public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("OpenTelemetry");
        var serviceName = section["ServiceName"] ?? "Product.Template.v2";
        var enableTraces = section.GetValue("EnableTraces", false);
        var enableMetrics = section.GetValue("EnableMetrics", false);

        if (!enableTraces && !enableMetrics)
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                if (!enableTraces)
                    return;

                tracing.AddAspNetCoreInstrumentation();
            })
            .WithMetrics(metrics =>
            {
                if (!enableMetrics)
                    return;

                metrics.AddAspNetCoreInstrumentation();
            });

        return services;
    }
}
