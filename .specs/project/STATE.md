# STATE — Product.Template v2

**Last updated:** 2026-08-03

## Current Status

| Area | Status | Notes |
|------|--------|-------|
| Scaffold v2.1 | ✅ Done | App + App.Tests + ArchitectureTests |
| Host layer | ✅ Done | Serilog, CORS, health, OpenAPI/Scalar |
| Feature flags | ✅ Done | RequireFeature + ChatAi stub |
| Identity (M2) | ✅ Done | Register, Login, Refresh |
| Authorization (M2) | ✅ Done | Roles, permissions, assignments, JWT claims |
| Tenants (M2) | ✅ Done | CRUD core + subdomain/header tenant resolution + seed |
| Pagination + tenant resolver | ✅ Done | Flat v1 pagination on all list endpoints; subdomain-first |
| PostgreSQL + seeders | ✅ Done | Npgsql runtime, InMemory tests, docker compose |
| SOLID refinements | ✅ Done | Module registration, contracts, tenant filter, behavior |
| AI full module | ✅ Done | AgentLoop, tools, ChatAi, StubLlmService |
| E2E tests | ✅ Done | Identity auth + health HTTP |
| Docker + CI/CD | ✅ Done | Dockerfile, compose, GitHub Actions + Trivy |

## M3 Gate ✅

```
dotnet build                          ✅
dotnet test tests/ArchitectureTests   ✅ 4/4
dotnet test tests/App.Tests           ✅ 75/75
dotnet test tests/E2ETests            ✅ 4/4
Program.cs                            ✅ ≤40 lines
```

## M2 Gate ✅

```
dotnet build                          ✅
dotnet test tests/ArchitectureTests   ✅ 4/4
dotnet test tests/App.Tests           ✅ 71/71
Program.cs                            ✅ ≤40 lines
```

## Decisions

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-08-03 | Paginação flat v1 (`PaginatedListOutput<T>`) | Consistência com v1; pageSize default 20, max 100 |
| 2026-08-03 | Subdomain-first tenant resolution | `SubdomainThenHeaderTenantResolver` + `MultiTenancyOptions` |
| 2026-08-03 | ListTenants exige tenant resolvido | Removido `ITenantExemptRequest` de ListTenants |
| 2026-08-03 | VSA v2.1 — slice = arquivo plano | Token efficiency |
| 2026-08-03 | `{Module}Module.cs` self-registration | OCP/DIP without multi-project |
| 2026-08-03 | `ITenantQueryFilterConfigurator` | Shared não referencia Features |
| 2026-08-03 | `AuthContracts` / `TenantContracts` | DTOs compartilhados no módulo |
| 2026-08-03 | `TenantContextBehavior` + `ITenantExemptRequest` | Fail-fast multi-tenant |
| 2026-08-03 | AI full = P3 / M3 | Fora MVP |
| 2026-08-03 | v2 = template greenfield | Sem migração v1→v2; v1 só referência funcional |

## Blockers

None.

## Next (M4)

- [x] Getting started greenfield (`docs/guides/getting-started.md`)
- [x] RBAC matrix v2 (`docs/security/RBAC_MATRIX.md`)
- [x] JWT hardening (security_stamp + tenant_id validation)
- [x] OAuth providers + external-login
- [ ] (Opcional) Azure OpenAI real, E2E Authorization HTTP expandido
