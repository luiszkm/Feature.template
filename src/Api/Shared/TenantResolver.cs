using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.Shared;

public sealed class MultiTenancyOptions
{
    public const string SectionName = "MultiTenancy";

    public string HeaderName { get; init; } = "X-Tenant";

    public string? BaseDomain { get; init; }

    public bool AllowDevFallback { get; init; } = true;

    public string DevTenantKey { get; init; } = "dev";
}

public interface ITenantResolver
{
    string? ResolveTenantKey(HttpContext httpContext);
}

public sealed class SubdomainThenHeaderTenantResolver(IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    private readonly MultiTenancyOptions _options = options.Value;

    public string? ResolveTenantKey(HttpContext httpContext)
    {
        var fromSubdomain = ResolveFromSubdomain(httpContext.Request.Host.Host);
        if (!string.IsNullOrWhiteSpace(fromSubdomain))
            return fromSubdomain;

        if (httpContext.Request.Headers.TryGetValue(_options.HeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim().ToLowerInvariant();
        }

        return null;
    }

    private string? ResolveFromSubdomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var normalizedHost = host.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(_options.BaseDomain))
        {
            var baseDomain = _options.BaseDomain.Trim().ToLowerInvariant();
            if (!normalizedHost.EndsWith(baseDomain, StringComparison.Ordinal))
                return null;

            var prefix = normalizedHost[..^baseDomain.Length].TrimEnd('.');
            if (string.IsNullOrWhiteSpace(prefix))
                return null;

            return prefix.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        var segments = normalizedHost.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 ? segments[0] : null;
    }
}
