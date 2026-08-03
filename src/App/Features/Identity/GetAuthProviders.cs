using App.Host.Security;

namespace App.Features.Identity;

public sealed record AuthProvidersOutput(IReadOnlyList<string> Providers, int Count);

public sealed class GetAuthProvidersEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/identity/providers", (IAuthenticationProviderFactory factory) =>
        {
            var providers = factory.GetAvailableProviders();
            return Results.Ok(new AuthProvidersOutput(providers, providers.Count));
        })
        .WithName("GetAuthProviders")
        .WithTags("Identity")
        .AllowAnonymous()
        .Produces<AuthProvidersOutput>(StatusCodes.Status200OK);
    }
}
