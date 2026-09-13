using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Authorization;

public sealed class RolePermission : Entity, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    public Role? Role { get; private set; }
    public Permission? Permission { get; private set; }

    private RolePermission() { }

    public static RolePermission Create(Guid roleId, Guid permissionId, Guid tenantId) =>
        new()
        {
            RoleId = roleId,
            PermissionId = permissionId,
            TenantId = tenantId,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> entity)
    {
        entity.ToTable("RolePermissions");
        entity.HasKey(rp => rp.Id);
        entity.HasIndex(rp => new { rp.TenantId, rp.RoleId, rp.PermissionId }).IsUnique();
        entity.HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
