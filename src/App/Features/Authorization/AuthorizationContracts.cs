namespace App.Features.Authorization;

public sealed record RoleWithPermissionsOutput(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<PermissionOutput> Permissions);

internal static class AuthorizationMapper
{
    public static RoleOutput ToOutput(this Role role) =>
        new(role.Id, role.Name, role.Description);

    public static PermissionOutput ToOutput(this Permission permission) =>
        new(permission.Id, permission.Name, permission.Description);

    public static RoleWithPermissionsOutput ToOutputWithPermissions(this Role role)
    {
        var permissions = role.RolePermissions
            .Where(rp => rp.Permission is not null)
            .Select(rp => rp.Permission!.ToOutput())
            .OrderBy(p => p.Name)
            .ToList();

        return new RoleWithPermissionsOutput(role.Id, role.Name, role.Description, permissions);
    }
}
