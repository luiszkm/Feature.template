using Api.Host.Security;
using Microsoft.Extensions.Options;

namespace Api.Features.Identity;

/// <summary>
/// The refresh token travels in a cookie the browser cannot read. Everything that writes, reads or
/// clears it goes through here, so the attributes are decided in one place.
/// </summary>
public static class RefreshCookie
{
    public const string Name = "pt_refresh";

    /// <summary>Narrow enough that no other route ever receives the credential.</summary>
    public const string Path = "/api/v1/identity";

    public static CookieOptions OptionsFor(HttpContext context, int expirationDays) => new()
    {
        HttpOnly = true,
        // A `Secure` cookie over http://localhost would be dropped by the browser, so the flag
        // follows the scheme the request actually arrived on.
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        MaxAge = TimeSpan.FromDays(expirationDays),
        IsEssential = true
    };

    public static void Write(HttpContext context, string rawToken)
    {
        var days = context.RequestServices
            .GetRequiredService<IOptions<JwtSettings>>()
            .Value.RefreshTokenExpirationDays;

        context.Response.Cookies.Append(Name, rawToken, OptionsFor(context, days));
    }

    public static string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrWhiteSpace(token)
            ? token
            : null;

    public static void Clear(HttpContext context)
    {
        var options = OptionsFor(context, 0);
        options.MaxAge = TimeSpan.Zero;
        context.Response.Cookies.Delete(Name, options);
    }
}
