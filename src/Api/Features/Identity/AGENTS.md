# AGENTS — Identity

Autenticação, utilizadores, refresh tokens e `security_stamp`.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `User.cs` | User + `IUserRepository` + EF config |
| `RefreshToken.cs` | RefreshToken + `IRefreshTokenRepository` + EF config |

## Slices (verbo)

Ver `features.json` com `"m": "Identity"`: RegisterUser, Login, RefreshAccessToken, GetUser, ListUsers, UpdateUser, DeleteUser, GetUserRoles.

## Infra do módulo

| Ficheiro | Papel |
|----------|-------|
| `IdentityModule.cs` | DI, tenant filters, policies |
| `IdentityAuthorization.cs` | SelfOrPermission handler |
| `IdentityPermissions.cs` | `identity.user.read`, `identity.user.manage` |
| `SecurityStampService.cs` | Regenerate + validate stamp (cache) |
| `AuthContracts.cs` | AuthTokenOutput, UserAuthOutput |
| `UserOutput.cs` | UserOutput + `UserMapper.ToOutput` |

## Policies

| Policy | Uso |
|--------|-----|
| `UsersRead` | ListUsers |
| `UsersManage` | DeleteUser, GetUserRoles |
| `UserReadOrSelf` | GetUser |
| `UserManageOrSelf` | UpdateUser |
| anonymous | Register, Login, Refresh |

Detalhe: `docs/security/RBAC_MATRIX.md`.

## Dependências cross-module

- **Authorization:** `IUserRolesProvider` (roles/permissions no JWT)
- **Authorization → Identity:** `ISecurityStampService` em assign/revoke role

## Gotchas

- `DeleteUser` faz soft-delete (`Deactivate`) + regenerate stamp
- Admin seed: `User.Create` por email (`Seed:AdminEmail`) — sem ID fixo
- JWT inclui `security_stamp` e `tenant_id`
- `RegisterUser` devolve `UserOutput` (não DTO duplicado)
- Refresh token: hash SHA-256 em DB; rotação no refresh

## Testes

```
tests/App.Tests/Identity/
  RegisterUserTests.cs
  LoginTests.cs
  RefreshAccessTokenTests.cs
  UserManagementTests.cs
  SecurityStampServiceTests.cs
tests/E2ETests/Identity/
```

Credenciais dev: ver `docs/guides/getting-started.md`.
