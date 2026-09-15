namespace Api.Host.Security;

public sealed class JwtSettings
{
    public bool Enabled { get; set; } = true;
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Product.Template";
    public string Audience { get; set; } = "Product.Template.Api";
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
