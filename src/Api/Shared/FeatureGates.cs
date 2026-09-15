using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace Api.Shared;

public sealed class FeatureGateFilter(string featureName) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var featureManager = context.HttpContext.RequestServices.GetRequiredService<IFeatureManager>();
        if (!await featureManager.IsEnabledAsync(featureName))
        {
            return Results.Json(
                new ProblemDetails
                {
                    Title = "Feature disabled",
                    Detail = $"The feature '{featureName}' is not enabled in this environment.",
                    Status = StatusCodes.Status404NotFound
                },
                statusCode: StatusCodes.Status404NotFound);
        }

        return await next(context);
    }
}

public static class FeatureGateExtensions
{
    public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string featureName) =>
        builder.AddEndpointFilter(new FeatureGateFilter(featureName));
}
