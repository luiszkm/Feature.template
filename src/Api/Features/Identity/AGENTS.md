# AGENTS — Identity

Autenticação, utilizadores, refresh tokens e `security_stamp`.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `User.cs` | User + `IUserRepository` + EF config |
| `RefreshToken.cs` | RefreshToken + `IRefreshTokenRepository` + EF config |

## Slices (verbo)

Ver `features.json` com `"m": "Identity"`: RegisterUser, Login, RefreshAccessToken, Logout, GetUser, ListUsers, UpdateUser, DeleteUser, GetUserRoles.

## Infra do módulo

| Ficheiro | Papel |
|----------|-------|
| `IdentityModule.cs` | DI, tenant filters, policies; `IUserLookup` + `IUserDirectory` |
| `IdentityAuthorization.cs` | SelfOrPermission handler |
| `IdentityPermissions.cs` | `identity.user.read`, `identity.user.manage` |
| `SecurityStampService.cs` | Implementação de `ISecurityStampService` (contrato em Shared) |
| `AuthContracts.cs` | `AuthTokenResponse` (o que vai na rede), `AuthTokenOutput` (saída do handler, com o token cru), `UserAuthOutput` |
| `RefreshCookie.cs` | Escreve, lê e apaga o cookie `pt_refresh` — único sítio que decide os atributos |
| `UserOutput.cs` | UserOutput + `UserMapper.ToOutput` |

## Policies

| Policy | Uso |
|--------|-----|
| `UsersRead` | ListUsers |
| `UsersManage` | DeleteUser, GetUserRoles |
| `UserReadOrSelf` | GetUser |
| `UserManageOrSelf` | UpdateUser |
| anonymous | Register, Login, Refresh, Logout |

Detalhe: `docs/security/RBAC_MATRIX.md`.

## Dependências cross-module

- **Authorization:** `IUserRolesProvider` (roles/permissions no JWT) — contrato em Shared
- **Authorization / Host / Ai:** `IUserLookup`, `ISecurityStampService` e `IUserDirectory` em Shared; Identity implementa. Assign/revoke regenera stamp sem importar este módulo

## Gotchas

- `DeleteUser` faz soft-delete (`Deactivate`) + regenerate stamp
- Admin seed: `User.Create` por email (`Seed:AdminEmail`) — sem ID fixo
- JWT inclui `security_stamp` e `tenant_id`
- `RegisterUser` devolve `UserOutput` (não DTO duplicado)
- Refresh token: hash SHA-256 em DB; rotação no refresh
- **O refresh token nunca vai no corpo.** Viaja no cookie `pt_refresh` (`HttpOnly`, `SameSite=Strict`, `Path=/api/v1/identity`, `Secure` quando HTTPS). `RefreshTokenEndpoint` só o aceita do cookie — um token no corpo teria de ser legível por script
- `Logout` é anónimo de propósito: um access token expirado não pode impedir alguém de revogar o refresh que já tem
- Mudou um endpoint? `UPDATE_OPENAPI=1 dotnet test tests/E2ETests` regenera `src/Api/openapi.json`

## Testes

```
tests/Api.Tests/Identity/
  RegisterUserTests.cs
  LoginTests.cs
  RefreshAccessTokenTests.cs
  LogoutTests.cs            # + RefreshCookieTests (atributos do cookie)
  UserManagementTests.cs
  SecurityStampServiceTests.cs
tests/E2ETests/Identity/    # cookie, rotação e revogação contra o host real
```

Credenciais dev: ver `docs/guides/getting-started.md`.
