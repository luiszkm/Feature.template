using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace App.Host.Security;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    public int GetExpiresInSeconds() =>
        (int)TimeSpan.FromMinutes(_settings.ExpirationMinutes).TotalSeconds;

    public string CreateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<Claim>? extraClaims = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured.");

        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (extraClaims is not null)
            claims.AddRange(extraClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(_settings.ExpirationMinutes),
            NotBefore = now,
            IssuedAt = now,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = creds
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public int GetRefreshTokenExpirationDays() => _settings.RefreshTokenExpirationDays;
}
