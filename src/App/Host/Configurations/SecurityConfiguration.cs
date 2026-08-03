using System.Security.Claims;
using System.Text;
using App.Features.Authorization;
using App.Features.Identity;
using App.Features.Tenants;
using App.Host.Security;
using App.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
namespace App.Host.Configurations;

public static class SecurityConfiguration
{
    public const string DefaultCorsPolicyName = "DefaultCorsPolicy";

    public static IServiceCollection AddSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        AddCors(services, configuration, environment);

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddHttpContextAccessor();
        services.AddAuthenticationProviders(configuration);

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
            throw new InvalidOperationException("Jwt:Secret must be configured.");

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

    private static void AddCors(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["*"];

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultCorsPolicyName, builder =>
            {
                if (allowedOrigins.Contains("*") || (allowedOrigins.Length == 0 && environment.IsDevelopment()))
                {
                    builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                builder.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
}
