# AGENTS — Authorization

RBAC: roles, permissions, assignments utilizador↔role.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `Role.cs` | Role + `IRoleRepository` + EF config |
| `Permission.cs` | Permission + `IPermissionRepository` |
| `RolePermission.cs` | Junção role↔permission |
| `UserAssignment.cs` | Junção user↔role |

## Slices (verbo)

Ver `features.json` com `"m": "Authorization"`: CreateRole, ListRoles, GetRole, UpdateRole, DeleteRole, CreatePermission, ListPermissions, UpdatePermission, DeletePermission, AssignPermissionToRole, RevokePermissionFromRole, AssignUserToRole, GetUserAssignments, RevokeUserFromRole.

## Infra do módulo

| Ficheiro | Papel |
|----------|-------|
| `AuthorizationModule.cs` | DI + policies |
| `AuthorizationPermissions.cs` | Permissões `authorization.*` |
| `AuthorizationContracts.cs` | RoleOutput, PermissionOutput, mappers |
| `UserRolesProvider.cs` | `IUserRolesProvider` — usado por Identity (Login/Refresh) |

## Policies

| Policy | Escopo |
|--------|--------|
| `AuthorizationRolesRead` / `Manage` | Roles + role permissions |
| `AuthorizationPermissionsRead` / `Manage` | Permissions CRUD |

Detalhe: `docs/security/RBAC_MATRIX.md`.

## Dependências cross-module

- **Identity:** `IUserRepository`, `ISecurityStampService` — assign/revoke role regenera stamp
- **Identity consome:** `IUserRolesProvider`

## Gotchas

- `Role.CreateWithId` existe para seed do Admin role (ID fixo no seeder)
- Permissões seed: `Host/Seeders/PermissionCatalog.cs`
- GetRole expõe duas rotas (role + permissions) no mesmo slice

## Testes

```
tests/App.Tests/Authorization/
  CreateRoleTests.cs
  AuthorizationManagementTests.cs
```
