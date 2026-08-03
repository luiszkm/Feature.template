using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Host.Security.Providers;

public sealed class MicrosoftAuthenticationProvider(
    IOptions<MicrosoftAuthSettings> options,
    HttpClient httpClient,
    ILogger<MicrosoftAuthenticationProvider> logger) : IAuthenticationProvider
{
    private readonly MicrosoftAuthSettings _settings = options.Value;

    public string ProviderName => "microsoft";

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Credentials.TryGetValue("code", out var authCode) || string.IsNullOrWhiteSpace(authCode))
        {
            return new AuthenticationResult(
                false,
                Error: "Authorization code is required for Microsoft authentication.");
        }

        try
        {
            var redirectUri = request.Credentials.GetValueOrDefault("redirectUri") ?? _settings.RedirectUri;
            var tokenResponse = await ExchangeCodeForTokenAsync(authCode, redirectUri, cancellationToken);
            if (tokenResponse is null)
                return new AuthenticationResult(false, Error: "Failed to obtain token from Microsoft.");

            var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken, cancellationToken);
            if (userInfo is null)
                return new AuthenticationResult(false, Error: "Failed to obtain user info from Microsoft.");

            logger.LogInformation("Microsoft authentication succeeded for user {Email}", userInfo.Email);

            return new AuthenticationResult(
                Success: true,
                AccessToken: tokenResponse.AccessToken,
                RefreshToken: tokenResponse.RefreshToken,
                ExpiresIn: tokenResponse.ExpiresIn,
                UserInfo: new Dictionary<string, string>
                {
                    ["email"] = userInfo.Email ?? userInfo.UserPrincipalName ?? string.Empty,
                    ["name"] = userInfo.DisplayName ?? string.Empty,
                    ["firstName"] = userInfo.GivenName ?? string.Empty,
                    ["lastName"] = userInfo.Surname ?? string.Empty,
                    ["microsoftId"] = userInfo.Id ?? string.Empty,
                    ["provider"] = ProviderName
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Microsoft authentication failed");
            return new AuthenticationResult(false, Error: $"Microsoft authentication error: {ex.Message}");
        }
    }

    private async Task<TokenResponse?> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var tokenEndpoint = $"{_settings.Authority}/oauth2/v2.0/token";

        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["scope"] = _settings.Scopes
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Microsoft token exchange failed. Status: {StatusCode}, Error: {Error}",
                response.StatusCode,
                errorContent);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
    }

    private async Task<MicrosoftUserInfo?> GetUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://graph.microsoft.com/v1.0/me");

        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Microsoft Graph /me failed. Status: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MicrosoftUserInfo>(cancellationToken);
    }

    private sealed record TokenResponse(
        string AccessToken,
        string TokenType,
        int ExpiresIn,
        string? RefreshToken,
        string Scope);

    private sealed record MicrosoftUserInfo(
        string Id,
        string? Mail,
        string? UserPrincipalName,
        string? DisplayName,
        string? GivenName,
        string? Surname)
    {
        public string? Email => Mail ?? UserPrincipalName;
    }
}
