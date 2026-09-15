using System.Security.Claims;

namespace Api.Shared;

public interface IJwtTokenService
{
    int GetExpiresInSeconds();
    string CreateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<Claim>? extraClaims = null);
    string GenerateRefreshToken();
    int GetRefreshTokenExpirationDays();
}
