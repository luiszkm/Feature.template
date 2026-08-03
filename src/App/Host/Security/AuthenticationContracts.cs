namespace App.Host.Security;

public interface IAuthenticationProvider
{
    string ProviderName { get; }
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAuthenticationProviderFactory
{
    IAuthenticationProvider GetProvider(string providerName);
    IReadOnlyList<string> GetAvailableProviders();
    bool IsProviderAvailable(string providerName);
}

public sealed record AuthenticationRequest(
    string Provider,
    Dictionary<string, string> Credentials);

public sealed record AuthenticationResult(
    bool Success,
    string? AccessToken = null,
    string? RefreshToken = null,
    int? ExpiresIn = null,
    string? Error = null,
    Dictionary<string, string>? UserInfo = null);
