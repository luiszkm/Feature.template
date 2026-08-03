using App.Host.Middleware;

namespace App.Host.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseHostPipeline(this WebApplication app)
    {
        app.UseMiddleware<TenantMiddleware>();
        return app;
    }
}
