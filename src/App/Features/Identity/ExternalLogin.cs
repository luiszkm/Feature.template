using System.Security.Claims;
using System.Security.Cryptography;
using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace App.Features.Identity;

public sealed record ExternalLoginCommand(
    string Provider,
    string Code,
    string? RedirectUri = null) : ICommand<AuthTokenOutput>;

public sealed class ExternalLoginValidator : AbstractValidator<ExternalLoginCommand>
{
    public ExternalLoginValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.RedirectUri).MaximumLength(500).When(x => x.RedirectUri is not null);
    }
}

public sealed class ExternalLoginHandler(
    IAuthenticationProviderFactory providerFactory,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IUserRolesProvider userRolesProvider,
    IConfiguration configuration) : IRequestHandler<ExternalLoginCommand, AuthTokenOutput>
{
    public async Task<AuthTokenOutput> Handle(ExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before external login.");

        if (!providerFactory.IsProviderAvailable(request.Provider))
            throw new UnauthorizedAccessException("Authentication provider is not supported.");

        var provider = providerFactory.GetProvider(request.Provider);

        var credentials = new Dictionary<string, string> { ["code"] = request.Code };
        if (!string.IsNullOrWhiteSpace(request.RedirectUri))
            credentials["redirectUri"] = request.RedirectUri;

        var authResult = await provider.AuthenticateAsync(
            new AuthenticationRequest(request.Provider, credentials),
            cancellationToken);

        if (!authResult.Success
            || authResult.UserInfo is null
            || !authResult.UserInfo.TryGetValue("email", out var email)
            || string.IsNullOrWhiteSpace(email))
        {
            throw new UnauthorizedAccessException(authResult.Error ?? "External authentication failed.");
        }

        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            if (!configuration.GetValue("Identity:AllowExternalLoginAutoProvision", true))
                throw new BusinessRuleException("User account does not exist. Contact an administrator.");

            var firstName = authResult.UserInfo.GetValueOrDefault("firstName", email.Split('@')[0]);
            var lastName = authResult.UserInfo.GetValueOrDefault("lastName", "External");

            // Unusable password: hashed cryptographically random secret that is never returned.
            var unusableSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            user = User.Create(
                tenantId,
                Email.Create(email),
                passwordHasher.Hash(unusableSecret),
                firstName,
                lastName);

            if (configuration.GetValue("Identity:ConfirmEmailOnExternalProvision", true))
                user.ConfirmEmail();

            await userRepository.AddAsync(user, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("External authentication failed.");

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

        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            tenantId,
            user.Id,
            rawRefreshToken,
            jwtTokenService.GetRefreshTokenExpirationDays(),
            "external-provider");

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

