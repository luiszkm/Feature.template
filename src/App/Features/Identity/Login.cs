using System.Security.Claims;
using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace App.Features.Identity;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokenOutput>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}

public sealed class LoginHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork,
    IHttpContextAccessor httpContextAccessor,
    ITenantContext tenantContext,
    IUserRolesProvider userRolesProvider) : IRequestHandler<LoginCommand, AuthTokenOutput>
{
    public async Task<AuthTokenOutput> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before login.");

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null
            || !passwordHasher.Verify(request.Password, user.PasswordHash)
            || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.EmailConfirmed)
            throw new BusinessRuleException("Email address must be confirmed before login.");

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

        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            tenantId,
            user.Id,
            rawRefreshToken,
            jwtTokenService.GetRefreshTokenExpirationDays(),
            clientIp);

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        user.UpdateLastLogin();
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthTokenOutput(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: jwtTokenService.GetExpiresInSeconds(),
            RefreshToken: rawRefreshToken,
            User: new UserAuthOutput(
                Id: user.Id,
                Email: user.Email.Value,
                FirstName: user.FirstName,
                LastLoginAt: user.LastLoginAt,
                Roles: rolesData.Roles.ToList()));
    }
}

public sealed class LoginEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/identity/login", async (
            LoginCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithTags("Identity")
        .AllowAnonymous()
        .Produces<AuthTokenOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
