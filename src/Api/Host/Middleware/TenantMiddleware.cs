using Api.Shared;
using Microsoft.Extensions.Options;

namespace Api.Host.Middleware;

public sealed class TenantMiddleware(
    RequestDelegate next,
    IWebHostEnvironment environment,
    IOptions<MultiTenancyOptions> options)
{
    private readonly MultiTenancyOptions _options = options.Value;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver tenantResolver,
        ITenantStore tenantStore)
    {
        TenantInfo? tenant = null;

        var tenantKey = tenantResolver.ResolveTenantKey(context);
        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            tenant = await tenantStore.GetByKeyAsync(tenantKey, context.RequestAborted);
        }
        else if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader)
                 && Guid.TryParse(tenantIdHeader, out var tenantId))
        {
            tenant = await tenantStore.GetByIdAsync(tenantId, context.RequestAborted);
        }
        else if (environment.IsDevelopment() && _options.AllowDevFallback)
        {
            tenant = await tenantStore.GetByKeyAsync(_options.DevTenantKey, context.RequestAborted)
                ?? await tenantStore.GetByIdAsync(WellKnownTenants.Development, context.RequestAborted);
        }

        if (tenant is { IsActive: true })
            SetTenant(context, tenant);

        await next(context);
    }

    private static void SetTenant(HttpContext context, TenantInfo tenant)
    {
        var tenantContext = context.RequestServices.GetRequiredService<ITenantContext>();
        if (tenantContext is TenantContext mutable)
            mutable.SetTenant(tenant.Id, tenant.TenantKey);
    }
}
