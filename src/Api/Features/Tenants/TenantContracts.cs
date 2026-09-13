using App.Shared;

namespace App.Features.Tenants;

public sealed record TenantOutput(
    Guid TenantId,
    string TenantKey,
    string DisplayName,
    string? ContactEmail,
    bool IsActive,
    TenantIsolationMode IsolationMode,
    DateTime CreatedAt);

public static class TenantMapper
{
    public static TenantOutput ToOutput(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.TenantKey,
            tenant.DisplayName,
            tenant.ContactEmail,
            tenant.IsActive,
            tenant.IsolationMode,
            tenant.CreatedAt);
}
