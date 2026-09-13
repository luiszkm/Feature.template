using Scalar.AspNetCore;

namespace Api.Host.Configurations;

public static class OpenApiHostConfiguration
{
    public static WebApplication MapOpenApiHost(this WebApplication app)
    {
        // The document itself is also served under Testing: that is how the contract test
        // regenerates `src/Api/openapi.json` without a database or a running deployment.
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            app.MapOpenApi();

        // The reference UI stays development-only.
        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("Product.Template v2 API");
            });
        }

        return app;
    }
}
