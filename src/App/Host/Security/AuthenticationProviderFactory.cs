namespace App.Host.Security;

public sealed class AuthenticationProviderFactory(IEnumerable<IAuthenticationProvider> providers) : IAuthenticationProviderFactory
{
    private readonly IReadOnlyList<IAuthenticationProvider> _providers = providers.ToList();

    public IAuthenticationProvider GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be empty.", nameof(providerName));

        var provider = _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        return provider ?? throw new InvalidOperationException(
            $"Authentication provider '{providerName}' not found. Available: {string.Join(", ", GetAvailableProviders())}");
    }

    public IReadOnlyList<string> GetAvailableProviders() =>
        _providers.Select(p => p.ProviderName).OrderBy(n => n).ToList();

    public bool IsProviderAvailable(string providerName) =>
        _providers.Any(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
