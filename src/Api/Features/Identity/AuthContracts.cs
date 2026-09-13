namespace Api.Features.Identity;

/// <summary>
/// What goes on the wire. The refresh token is deliberately absent: it travels in the
/// `pt_refresh` cookie, out of reach of any script in the browser.
/// </summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserAuthOutput User);

/// <summary>Handler output. Carries the raw refresh token so the endpoint can set the cookie.</summary>
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
