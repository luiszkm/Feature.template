# AGENTS — Tenants

Multi-tenancy: CRUD de tenants e resolução de contexto.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `Tenant.cs` | Tenant + `ITenantRepository` + EF config |

## Slices (verbo)

Ver `features.json` com `"m": "Tenants"`: CreateTenant, ListTenants, GetTenant, UpdateTenant, DeactivateTenant.

## Infra do módulo

| Ficheiro | Papel |
|----------|-------|
| `TenantsModule.cs` | DI + policies; `ITenantStore` |
| `TenantsPermissions.cs` | `tenants.read`, `tenants.manage` |
| `TenantContracts.cs` | DTOs de tenant |

## Policies

| Policy | Uso |
|--------|-----|
| `TenantsRead` | List, Get |
| `TenantsManage` | Create, Update, Deactivate |

## Well-known tenants

- `WellKnownTenants.Development` — tenant dev (`X-Tenant: dev`)
- Seed: `Host/Seeders/TenantSeeder.cs`
- `Tenant.CreateWithId` usado no seed (ID fixo para tenant dev)

## Gotchas

- `DeactivateTenant` é soft-delete
- Resolução tenant: header `X-Tenant` / `X-Tenant-Id` ou subdomínio (`MultiTenancy:BaseDomain`)
- JWT claim `tenant_id` deve coincidir com tenant resolvido

## Testes

```
tests/App.Tests/Tenants/
  CreateTenantTests.cs
  TenantManagementTests.cs
```
