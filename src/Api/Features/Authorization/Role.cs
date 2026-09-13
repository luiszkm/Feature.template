using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Authorization;

public sealed class Role : AggregateRoot, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    private Role() { }

    public static Role Create(Guid tenantId, string name, string description)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name cannot be empty.", nameof(name));

        return new Role
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Role CreateWithId(
        Guid id,
        Guid tenantId,
        string name,
        string description)
    {
        var role = Create(tenantId, name, description);
        role.Id = id;
        return role;
    }

    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    public void AssignPermission(Guid permissionId)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permissionId))
            return;

        _rolePermissions.Add(RolePermission.Create(Id, permissionId, TenantId));
    }

    public void RevokePermission(Guid permissionId)
    {
        var rolePermission = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (rolePermission is not null)
            _rolePermissions.Remove(rolePermission);
    }
}

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<Role>> ListAllAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(Role role, CancellationToken cancellationToken = default);
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteAsync(Role role, CancellationToken cancellationToken = default);
    Task AssignPermissionAsync(
        Guid roleId,
        Guid permissionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
    Task RevokePermissionAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default);
}

internal sealed class RoleRepository(AppDbContext db) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<Role>(), id, cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return db.Set<Role>().FirstOrDefaultAsync(
            r => r.Name.ToLower() == normalized.ToLower(),
            cancellationToken);
    }

    public Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<Role>()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PaginatedListOutput<Role>> ListAllAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var query = db.Set<Role>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(listQuery.SearchTerm))
        {
            var term = listQuery.SearchTerm.Trim();
            query = query.Where(r =>
                r.Name.Contains(term) ||
                r.Description.Contains(term));
        }

        query = ApplySort(query, listQuery.SortBy, listQuery.SortDirection);

        return await query.ToPaginatedListAsync(listQuery, cancellationToken);
    }

    private static IQueryable<Role> ApplySort(IQueryable<Role> query, string? sortBy, string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id);

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "name" => descending
                ? query.OrderByDescending(r => r.Name).ThenBy(r => r.Id)
                : query.OrderBy(r => r.Name).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
        };
    }

    public Task AddAsync(Role role, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<Role>(), role, cancellationToken);

    public Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        if (db.Entry(role).State == EntityState.Detached)
            EfRepositoryHelpers.Update(db.Set<Role>(), role);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Role role, CancellationToken cancellationToken = default)
    {
        db.Set<Role>().Remove(role);
        return Task.CompletedTask;
    }

    public async Task AssignPermissionAsync(
        Guid roleId,
        Guid permissionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Set<RolePermission>()
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken);

        if (exists)
            return;

        await db.Set<RolePermission>()
            .AddAsync(RolePermission.Create(roleId, permissionId, tenantId), cancellationToken);
    }

    public async Task RevokePermissionAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var rolePermission = await db.Set<RolePermission>()
            .FirstOrDefaultAsync(
                rp => rp.RoleId == roleId && rp.PermissionId == permissionId,
                cancellationToken);

        if (rolePermission is not null)
            db.Set<RolePermission>().Remove(rolePermission);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("Roles");
        entity.HasKey(r => r.Id);
        entity.Property(r => r.Name).HasMaxLength(50).IsRequired();
        entity.Property(r => r.Description).HasMaxLength(250);
        entity.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
        entity.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(r => r.RolePermissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
