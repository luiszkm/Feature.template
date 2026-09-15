using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Api.Features.Authorization;
using Api.Features.Identity;
using Api.Features.Tenants;
using Api.Host.Security;
using Api.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Api.Host.Configurations;

public static class SecurityConfiguration
{
    public const string DefaultCorsPolicyName = "DefaultCorsPolicy";

    public static IServiceCollection AddSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        AddCors(services, configuration, environment);
        AddAuthRateLimiting(services);

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddHttpContextAccessor();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(SecurityPolicies.Authenticated, policy =>
                policy.RequireAuthenticatedUser());

            options.AddAuthorizationModulePolicies();
            options.AddIdentityModulePolicies();
            options.AddTenantsModulePolicies();
        });

        if (!configuration.GetValue("Jwt:Enabled", true))
            return services;

        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(
                "Jwt:Secret must be configured when Jwt:Enabled is true. " +
                "Set Jwt:Secret via environment, user-secrets, or environment-specific appsettings.");

        if (secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                        {
                            context.Fail("Invalid token: missing user identifier.");
                            return;
                        }

                        var stampClaim = context.Principal?.FindFirstValue(AuthorizationClaimTypes.SecurityStamp);
                        if (string.IsNullOrWhiteSpace(stampClaim))
                        {
                            context.Fail("Invalid token: missing security stamp.");
                            return;
                        }

                        var tenantClaim = context.Principal?.FindFirstValue(AuthorizationClaimTypes.TenantId);
                        if (tenantClaim is null || !Guid.TryParse(tenantClaim, out var tokenTenantId))
                        {
                            context.Fail("Invalid token: missing tenant identifier.");
                            return;
                        }

                        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
                        if (tenantContext.TenantId != tokenTenantId)
                        {
                            context.Fail("Invalid token: tenant mismatch.");
                            return;
                        }

                        var stampService = context.HttpContext.RequestServices.GetRequiredService<ISecurityStampService>();
                        var isValid = await stampService.ValidateAsync(
                            tokenTenantId,
                            userId,
                            stampClaim,
                            context.HttpContext.RequestAborted);

                        if (!isValid)
                            context.Fail("Security stamp validation failed.");
                    }
                };
            });

        return services;
    }

    private static void AddAuthRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(RateLimitPolicies.AuthRateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = 20;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });
    }

    private static void AddCors(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var allowAnyOrigin = allowedOrigins.Contains("*", StringComparer.Ordinal);

        if (allowAnyOrigin && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins '*' is not allowed outside Development/Testing.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultCorsPolicyName, builder =>
            {
                if (allowAnyOrigin)
                {
                    builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                if (allowedOrigins.Length == 0)
                {
                    // Deny cross-origin by default; override via Cors:AllowedOrigins.
                    builder.SetIsOriginAllowed(_ => false)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    return;
                }

                builder.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
}
