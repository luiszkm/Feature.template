using App.Host.Configurations;
using App.Shared;
using MediatR;

namespace App.Features.Identity;

public sealed class ExternalLoginEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/identity/external-login", async (
            ExternalLoginCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ExternalLogin")
        .WithTags("Identity")
        .AllowAnonymous()
        .RequireRateLimiting(SecurityConfiguration.AuthRateLimitPolicy)
        .Produces<AuthTokenOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
