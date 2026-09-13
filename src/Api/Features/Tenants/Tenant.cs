using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Tenants;

public sealed class Tenant : AggregateRoot
{
    public string TenantKey { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? ContactEmail { get; private set; }
    public bool IsActive { get; private set; } = true;
    public TenantIsolationMode IsolationMode { get; private set; } = TenantIsolationMode.SharedDb;

    private Tenant() { }

    public static Tenant Create(
        string tenantKey,
        string displayName,
        string? contactEmail,
        TenantIsolationMode isolationMode)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
            throw new ArgumentException("TenantKey cannot be empty.", nameof(tenantKey));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

        return new Tenant
        {
            TenantKey = tenantKey.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            ContactEmail = contactEmail?.Trim(),
            IsActive = true,
            IsolationMode = isolationMode,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Tenant CreateWithId(
        Guid id,
        string tenantKey,
        string displayName,
        string? contactEmail,
        TenantIsolationMode isolationMode)
    {
        var tenant = Create(tenantKey, displayName, contactEmail, isolationMode);
        tenant.Id = id;
        return tenant;
    }

    public void Update(string displayName, string? contactEmail)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

        DisplayName = displayName.Trim();
        ContactEmail = contactEmail?.Trim();
    }

    public void Deactivate() => IsActive = false;
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<Tenant>> ListAllAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

internal sealed class TenantRepository(AppDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<Tenant>(), tenantId, cancellationToken);

    public Task<Tenant?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var normalized = tenantKey.Trim().ToLowerInvariant();
        return db.Set<Tenant>().FirstOrDefaultAsync(t => t.TenantKey == normalized, cancellationToken);
    }

    public async Task<PaginatedListOutput<Tenant>> ListAllAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var query = db.Set<Tenant>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(listQuery.SearchTerm))
        {
            var term = listQuery.SearchTerm.Trim();
            query = query.Where(t =>
                t.TenantKey.Contains(term) ||
                t.DisplayName.Contains(term));
        }

        query = ApplySort(query, listQuery.SortBy, listQuery.SortDirection);

        return await query.ToPaginatedListAsync(listQuery, cancellationToken);
    }

    private static IQueryable<Tenant> ApplySort(IQueryable<Tenant> query, string? sortBy, string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id);

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "tenantkey" or "key" => descending
                ? query.OrderByDescending(t => t.TenantKey).ThenBy(t => t.Id)
                : query.OrderBy(t => t.TenantKey).ThenBy(t => t.Id),
            "name" or "displayname" => descending
                ? query.OrderByDescending(t => t.DisplayName).ThenBy(t => t.Id)
                : query.OrderBy(t => t.DisplayName).ThenBy(t => t.Id),
            _ => query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
        };
    }

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<Tenant>(), tenant, cancellationToken);

    public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        EfRepositoryHelpers.Update(db.Set<Tenant>(), tenant);
        return Task.CompletedTask;
    }
}

internal sealed class TenantStore(ITenantRepository tenantRepository) : ITenantStore
{
    public async Task<TenantInfo?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByKeyAsync(tenantKey, cancellationToken);
        return Map(tenant);
    }

    public async Task<TenantInfo?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        return Map(tenant);
    }

    private static TenantInfo? Map(Tenant? tenant) =>
        tenant is null ? null : new TenantInfo(tenant.Id, tenant.TenantKey, tenant.IsActive);
}

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.ToTable("Tenants");
        entity.HasKey(t => t.Id);
        entity.Property(t => t.TenantKey).HasMaxLength(64).IsRequired();
        entity.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
        entity.Property(t => t.ContactEmail).HasMaxLength(255);
        entity.HasIndex(t => t.TenantKey).IsUnique();
    }
}
