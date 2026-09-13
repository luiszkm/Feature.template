using Api.Features.Authorization;
using Api.Features.Identity;
using Api.Features.Tenants;

namespace Api.Host.Seeders;

internal static class PermissionCatalog
{
    public static IReadOnlyList<(string Name, string Description)> All { get; } =
    [
        (IdentityPermissions.UserRead, "Read users and basic details"),
        (IdentityPermissions.UserManage, "Manage users (create/update/delete)"),
        (AuthorizationPermissions.RoleRead, "Read roles and their permission assignments"),
        (AuthorizationPermissions.RoleManage, "Create, update, delete roles and manage permission assignments"),
        (AuthorizationPermissions.PermissionRead, "Read permissions catalog"),
        (AuthorizationPermissions.PermissionManage, "Create, update, delete permissions"),
        (TenantsPermissions.Read, "Read tenants and their details"),
        (TenantsPermissions.Manage, "Create, update, and deactivate tenants")
    ];
}
