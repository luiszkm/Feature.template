using Api.Host.Middleware;

namespace Api.Host.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseHostPipeline(this WebApplication app)
    {
        app.UseMiddleware<TenantMiddleware>();
        return app;
    }
}
