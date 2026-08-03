using App.Host;
using App.Host.Middleware;

namespace App.Host.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseHostPipeline(this WebApplication app)
    {
        app.UseMiddleware<TenantMiddleware>();

        // TODO(M2): RequestLoggingMiddleware when EnableAdvancedLogging
        _ = app.Configuration.GetValue<bool>($"FeatureFlags:{FeatureFlags.EnableAdvancedLogging}", true);

        // TODO(M2): RequestDeduplicationMiddleware when EnableRequestDeduplication
        _ = app.Configuration.GetValue<bool>($"FeatureFlags:{FeatureFlags.EnableRequestDeduplication}", true);

        return app;
    }
}
