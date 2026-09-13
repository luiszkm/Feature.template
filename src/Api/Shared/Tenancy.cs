namespace Api.Shared;

public enum TenantIsolationMode
{
    SharedDb = 0,
    SchemaPerTenant = 1,
    DedicatedDb = 2
}

public static class WellKnownTenants
{
    public static readonly Guid Public = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static readonly Guid Development = Guid.Parse("11111111-1111-1111-1111-111111111111");
}

public interface ITenantStore
{
    Task<TenantInfo?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<TenantInfo?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TenantInfo(Guid Id, string TenantKey, bool IsActive);
