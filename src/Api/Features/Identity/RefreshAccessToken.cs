using System.Security.Claims;
using Api.Host.Configurations;
using FluentValidation;
using MediatR;

namespace Api.Features.Identity;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokenOutput>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(500);
    }
}

public sealed class RefreshTokenHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork,
    IHttpContextAccessor httpContextAccessor,
    ITenantContext tenantContext,
    IUserRolesProvider userRolesProvider) : IRequestHandler<RefreshTokenCommand, AuthTokenOutput>
{
    public async Task<AuthTokenOutput> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before refreshing tokens.");

        var existing = await refreshTokenRepository.GetActiveByTokenAsync(request.RefreshToken, cancellationToken);
        if (existing is null || !existing.IsActive)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        if (existing.TenantId != tenantId)
            throw new UnauthorizedAccessException("Refresh token belongs to another tenant.");

        var user = await userRepository.GetByIdAsync(existing.UserId, cancellationToken)
            ?? throw new NotFoundException($"User '{existing.UserId}' was not found.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var newRawToken = jwtTokenService.GenerateRefreshToken();

        var revoked = await refreshTokenRepository.TryRevokeAsync(
            request.RefreshToken,
            clientIp,
            newRawToken,
            cancellationToken);

        if (!revoked)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        var newRefreshToken = RefreshToken.Create(
            tenantId,
            user.Id,
            newRawToken,
            jwtTokenService.GetRefreshTokenExpirationDays(),
            clientIp);

        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);

        var rolesData = await userRolesProvider.GetUserRolesAndPermissionsAsync(user.Id, cancellationToken);

        var permissionClaims = rolesData.Permissions
            .Select(p => new Claim(AuthorizationClaimTypes.Permission, p));

        var extraClaims = permissionClaims
            .Append(new Claim(AuthorizationClaimTypes.SecurityStamp, user.SecurityStamp))
            .Append(new Claim(AuthorizationClaimTypes.TenantId, tenantId.ToString()));

        var accessToken = jwtTokenService.CreateAccessToken(
            user.Id,
            user.Email.Value,
            rolesData.Roles,
            extraClaims);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthTokenOutput(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: jwtTokenService.GetExpiresInSeconds(),
            RefreshToken: newRawToken,
            User: new UserAuthOutput(
                Id: user.Id,
                Email: user.Email.Value,
                FirstName: user.FirstName,
                LastLoginAt: user.LastLoginAt,
                Roles: rolesData.Roles.ToList()));
    }
}

public sealed class RefreshTokenEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/identity/refresh", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            // The cookie is the only accepted source. A token in the body would have to be
            // readable by script, which is what the cookie exists to prevent.
            var refreshToken = RefreshCookie.Read(context)
                ?? throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

            var result = await mediator.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            RefreshCookie.Write(context, result.RefreshToken);

            return Results.Ok(new AuthTokenResponse(
                result.AccessToken,
                result.TokenType,
                result.ExpiresIn,
                result.User));
        })
        .WithName("RefreshToken")
        .WithTags("Identity")
        .AllowAnonymous()
        .RequireRateLimiting(SecurityConfiguration.AuthRateLimitPolicy)
        .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
