using App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Features.Authorization;

public sealed class Permission : Entity, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private Permission() { }

    public static Permission Create(Guid tenantId, string name, string description)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Permission name cannot be empty.", nameof(name));

        return new Permission
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Permission name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }
}

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Permission?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<Permission>> ListAllAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(Permission permission, CancellationToken cancellationToken = default);
    Task UpdateAsync(Permission permission, CancellationToken cancellationToken = default);
    Task DeleteAsync(Permission permission, CancellationToken cancellationToken = default);
}

internal sealed class PermissionRepository(AppDbContext db) : IPermissionRepository
{
    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<Permission>(), id, cancellationToken);

    public Task<Permission?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return db.Set<Permission>().FirstOrDefaultAsync(
            p => p.Name.ToLower() == normalized.ToLower(),
            cancellationToken);
    }

    public async Task<PaginatedListOutput<Permission>> ListAllAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var query = db.Set<Permission>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(listQuery.SearchTerm))
        {
            var term = listQuery.SearchTerm.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                p.Description.Contains(term));
        }

        query = ApplySort(query, listQuery.SortBy, listQuery.SortDirection);

        return await query.ToPaginatedListAsync(listQuery, cancellationToken);
    }

    private static IQueryable<Permission> ApplySort(IQueryable<Permission> query, string? sortBy, string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id);

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "name" => descending
                ? query.OrderByDescending(p => p.Name).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };
    }

    public Task AddAsync(Permission permission, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<Permission>(), permission, cancellationToken);

    public Task UpdateAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        EfRepositoryHelpers.Update(db.Set<Permission>(), permission);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        db.Set<Permission>().Remove(permission);
        return Task.CompletedTask;
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> entity)
    {
        entity.ToTable("Permissions");
        entity.HasKey(p => p.Id);
        entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
        entity.Property(p => p.Description).HasMaxLength(250);
        entity.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
