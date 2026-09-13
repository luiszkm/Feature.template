using Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Authorization;

public sealed class UserRolesProvider(AppDbContext db, ITenantContext tenantContext) : IUserRolesProvider
{
    public async Task<UserRolesData> GetUserRolesAndPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before loading user roles.");

        var assignments = await db.Set<UserAssignment>()
            .Where(ua => ua.UserId == userId && ua.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();
        if (roleIds.Count == 0)
            return new UserRolesData([], []);

        var roles = await db.Set<Role>()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .Where(r => roleIds.Contains(r.Id) && r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var roleNames = roles
            .Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissionNames = roles
            .SelectMany(r => r.RolePermissions)
            .Where(rp => rp.Permission is not null)
            .Select(rp => rp.Permission!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UserRolesData(roleNames, permissionNames);
    }
}
