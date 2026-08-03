namespace App.Host.Security;

public sealed class MicrosoftAuthSettings
{
    public bool Enabled { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string TenantId { get; init; } = "common";
    public string RedirectUri { get; init; } = string.Empty;
    public string Scopes { get; init; } = "openid profile email User.Read";
    public string Authority => $"https://login.microsoftonline.com/{TenantId}";
}
