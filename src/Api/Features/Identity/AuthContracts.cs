namespace App.Features.Identity;

public sealed record AuthTokenOutput(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string RefreshToken,
    UserAuthOutput User);

public sealed record UserAuthOutput(
    Guid Id,
    string Email,
    string FirstName,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);
