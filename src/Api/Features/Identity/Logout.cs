using Api.Host.Configurations;
using Api.Shared;
using MediatR;

namespace Api.Features.Identity;

public sealed record LogoutCommand(string? RefreshToken) : ICommand<bool>;

public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IHttpContextAccessor httpContextAccessor) : IRequestHandler<LogoutCommand, bool>
{
    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // No cookie means there is nothing to revoke: the caller is already signed out as far as
        // this endpoint can tell, and saying so is not an error.
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return false;

        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var revoked = await refreshTokenRepository.TryRevokeAsync(
            request.RefreshToken,
            clientIp,
            replacedByRawToken: null,
            cancellationToken);

        if (revoked)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return revoked;
    }
}

public sealed class LogoutEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/identity/logout", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new LogoutCommand(RefreshCookie.Read(context)), cancellationToken);
            RefreshCookie.Clear(context);

            return Results.NoContent();
        })
        .WithName("Logout")
        .WithTags("Identity")
        // Anonymous on purpose: an expired access token must not stop someone from revoking the
        // refresh token they already hold.
        .AllowAnonymous()
        .RequireRateLimiting(SecurityConfiguration.AuthRateLimitPolicy)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
