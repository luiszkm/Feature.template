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
    private readonly byte[] _secretKey = Encoding.UTF8.GetBytes(
        options.Value.Secret.Length > 0
            ? options.Value.Secret
            : throw new InvalidOperationException("Jwt:Secret is required for email confirmation tokens."));

    public string GenerateToken(Guid userId, string securityStamp)
    {
        var payload = $"{userId:N}:{securityStamp}";
        var hash = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    public bool ValidateToken(Guid userId, string securityStamp, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var expected = GenerateToken(userId, securityStamp);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        return expectedBytes.Length == tokenBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, tokenBytes);
    }
}
