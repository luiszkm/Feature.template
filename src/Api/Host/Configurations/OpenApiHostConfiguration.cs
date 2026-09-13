using Scalar.AspNetCore;

namespace App.Host.Configurations;

public static class OpenApiHostConfiguration
{
    public static WebApplication MapOpenApiHost(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Product.Template v2 API");
        });

        return app;
    }
}
