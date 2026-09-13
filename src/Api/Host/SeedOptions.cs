namespace App.Host;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public const string DefaultDevPassword = "Admin@123";

    public string AdminEmail { get; init; } = "admin@producttemplate.com";

    public string AdminFirstName { get; init; } = "System";

    public string AdminLastName { get; init; } = "Administrator";

    /// <summary>Override via user-secrets or env var Seed__AdminPassword.</summary>
    public string? AdminPassword { get; init; }
}
