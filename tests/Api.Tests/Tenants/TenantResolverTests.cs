using Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.Tests.Tenants;

public sealed class TenantResolverTests
{
    [Fact]
    public void ResolveTenantKey_ShouldPreferSubdomain_OverHeader()
    {
        var resolver = CreateResolver(new MultiTenancyOptions { BaseDomain = "api.example.com" });
        var context = CreateContext("acme.api.example.com", tenantHeader: "other");

        var tenantKey = resolver.ResolveTenantKey(context);

        Assert.Equal("acme", tenantKey);
    }

    [Fact]
    public void ResolveTenantKey_ShouldUseHeader_WhenSubdomainNotPresent()
    {
        var resolver = CreateResolver(new MultiTenancyOptions());
        var context = CreateContext("localhost", tenantHeader: "dev");

        var tenantKey = resolver.ResolveTenantKey(context);

        Assert.Equal("dev", tenantKey);
    }

    [Fact]
    public void ResolveTenantKey_ShouldExtractFromThreeSegmentHost_WhenBaseDomainNotConfigured()
    {
        var resolver = CreateResolver(new MultiTenancyOptions());
        var context = CreateContext("acme.api.example.com");

        var tenantKey = resolver.ResolveTenantKey(context);

        Assert.Equal("acme", tenantKey);
    }

    [Fact]
    public void ResolveTenantKey_ShouldReturnNull_WhenHostHasFewerThanThreeSegments()
    {
        var resolver = CreateResolver(new MultiTenancyOptions());
        var context = CreateContext("example.com");

        var tenantKey = resolver.ResolveTenantKey(context);

        Assert.Null(tenantKey);
    }

    [Fact]
    public void ResolveTenantKey_ShouldUseCustomHeaderName()
    {
        var resolver = CreateResolver(new MultiTenancyOptions { HeaderName = "X-Custom-Tenant" });
        var context = CreateContext("localhost");
        context.Request.Headers["X-Custom-Tenant"] = "custom";

        var tenantKey = resolver.ResolveTenantKey(context);

        Assert.Equal("custom", tenantKey);
    }

    private static SubdomainThenHeaderTenantResolver CreateResolver(MultiTenancyOptions options) =>
        new(Options.Create(options));

    private static DefaultHttpContext CreateContext(string host, string? tenantHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);

        if (!string.IsNullOrWhiteSpace(tenantHeader))
            context.Request.Headers["X-Tenant"] = tenantHeader;

        return context;
    }
}
