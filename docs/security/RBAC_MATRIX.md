# RBAC Matrix — Product.Template v2

Matriz de autorização por endpoint. Toda rota protegida usa `[RequireAuthorization(Policy = "...")]` — nunca `[Authorize]` bare.

> Validação JWT pré-policy: claims obrigatórias `security_stamp` e `tenant_id`; stamp validado contra DB (`ISecurityStampService`); tenant do token deve coincidir com tenant resolvido.

## Policies

| Policy | Regra |
|--------|--------|
| `Authenticated` | Utilizador autenticado (JWT válido) |
| `UsersRead` | Role `Admin` **ou** claim `permission=identity.user.read` |
| `UsersManage` | Role `Admin` **ou** claim `permission=identity.user.manage` |
| `UserReadOrSelf` | `UsersRead` **ou** `userId` da rota = claim `NameIdentifier` |
| `UserManageOrSelf` | `UsersManage` **ou** self |
| `AuthorizationRolesRead` | Role `Admin` **ou** `permission=authorization.role.read` |
| `AuthorizationRolesManage` | Role `Admin` **ou** `permission=authorization.role.manage` |
| `AuthorizationPermissionsRead` | Role `Admin` **ou** `permission=authorization.permission.read` |
| `AuthorizationPermissionsManage` | Role `Admin` **ou** `permission=authorization.permission.manage` |
| `TenantsRead` | Role `Admin` **ou** `permission=tenants.read` |
| `TenantsManage` | Role `Admin` **ou** `permission=tenants.manage` |

## Identity

| Método | Rota | Acesso | Policy |
|--------|------|--------|--------|
| POST | `/api/v1/identity/login` | Público | — |
| POST | `/api/v1/identity/refresh` | Público | — |
| POST | `/api/v1/identity/register` | Público | — |
| GET | `/api/v1/identity/users` | Protegido | `UsersRead` |
| GET | `/api/v1/identity/users/{userId}` | Protegido | `UserReadOrSelf` |
| GET | `/api/v1/identity/users/{userId}/roles` | Protegido | `UsersManage` |
| PUT | `/api/v1/identity/users/{userId}` | Protegido | `UserManageOrSelf` |
| DELETE | `/api/v1/identity/users/{userId}` | Protegido | `UsersManage` |

## Authorization

| Método | Rota | Policy |
|--------|------|--------|
| GET | `/api/v1/authorization/roles` | `AuthorizationRolesRead` |
| GET | `/api/v1/authorization/roles/{roleId}` | `AuthorizationRolesRead` |
| POST | `/api/v1/authorization/roles` | `AuthorizationRolesManage` |
| PUT | `/api/v1/authorization/roles/{roleId}` | `AuthorizationRolesManage` |
| DELETE | `/api/v1/authorization/roles/{roleId}` | `AuthorizationRolesManage` |
| GET | `/api/v1/authorization/roles/{roleId}/permissions` | `AuthorizationRolesRead` |
| POST | `/api/v1/authorization/roles/{roleId}/permissions` | `AuthorizationRolesManage` |
| DELETE | `/api/v1/authorization/roles/{roleId}/permissions/{permissionId}` | `AuthorizationRolesManage` |
| GET | `/api/v1/authorization/permissions` | `AuthorizationPermissionsRead` |
| POST | `/api/v1/authorization/permissions` | `AuthorizationPermissionsManage` |
| PUT | `/api/v1/authorization/permissions/{permissionId}` | `AuthorizationPermissionsManage` |
| DELETE | `/api/v1/authorization/permissions/{permissionId}` | `AuthorizationPermissionsManage` |
| GET | `/api/v1/authorization/users/{userId}/roles` | `AuthorizationRolesRead` |
| POST | `/api/v1/authorization/users/{userId}/roles` | `AuthorizationRolesManage` |
| DELETE | `/api/v1/authorization/users/{userId}/roles/{roleId}` | `AuthorizationRolesManage` |

## Tenants

| Método | Rota | Policy |
|--------|------|--------|
| GET | `/api/v1/tenants` | `TenantsRead` |
| GET | `/api/v1/tenants/{id}` | `TenantsRead` |
| POST | `/api/v1/tenants` | `TenantsManage` |
| PUT | `/api/v1/tenants/{id}` | `TenantsManage` |
| DELETE | `/api/v1/tenants/{id}` | `TenantsManage` |

## AI

| Método | Rota | Policy | Feature flag |
|--------|------|--------|--------------|
| POST | `/api/v1/ai/chat` | `Authenticated` | `EnableAI` |

## Regras de revisão

1. Endpoint protegido sem policy explícita → rejeitar PR.
2. Endpoint novo → atualizar esta matriz no mesmo PR.
3. Permissões canônicas: `{module}.{resource}.{action}` (lowercase, dot-separated).
