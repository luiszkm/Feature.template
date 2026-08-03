using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using App.Host.Security;
using Microsoft.Extensions.Options;

namespace App.Features.Identity;

public interface IEmailConfirmationTokenService
{
    string GenerateToken(Guid userId, string securityStamp);
    bool ValidateToken(Guid userId, string securityStamp, string token);
}

internal sealed class EmailConfirmationTokenService(IOptions<JwtSettings> options) : IEmailConfirmationTokenService
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private readonly byte[] _secretKey = Encoding.UTF8.GetBytes(
        options.Value.Secret.Length > 0
            ? options.Value.Secret
            : throw new InvalidOperationException("Jwt:Secret is required for email confirmation tokens."));

    public string GenerateToken(Guid userId, string securityStamp)
    {
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds();
        var payload = $"{userId:N}:{securityStamp}:{expiresAtUnix.ToString(CultureInfo.InvariantCulture)}";
        var hash = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToBase64String(hash);
        return $"{expiresAtUnix.ToString(CultureInfo.InvariantCulture)}.{signature}";
    }

    public bool ValidateToken(Guid userId, string securityStamp, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
            return false;

        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAtUnix))
            return false;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
        if (expiresAt < DateTimeOffset.UtcNow)
            return false;

        var payload = $"{userId:N}:{securityStamp}:{expiresAtUnix.ToString(CultureInfo.InvariantCulture)}";
        var hash = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToBase64String(hash);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(parts[1]);

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
