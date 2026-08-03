# Roadmap — Product.Template v2

**Current Milestone:** M4 — Polish (optional)  
**Status:** M3 Complete ✅

---

## M1 — Foundation + Identity Pilot ✅

- VSA scaffold v2.1, RegisterUser, Host, Feature Flags, Harness LLM

---

## M2 — Core Modules ✅

**Goal:** Identity auth + Authorization + Tenants + Host hardening

### Delivered

**Identity**

- [x] Login + RefreshToken (JWT + rotation)
- [x] PBKDF2 password hasher
- [x] EmailConfirmed gate

**Authorization**

- [x] Role, Permission, UserAssignment
- [x] CreateRole, ListRoles, AssignUserToRole, AssignPermissionToRole, CreatePermission
- [x] IUserRolesProvider → JWT roles + permission claims
- [x] RBAC policies

**Tenants**

- [x] CreateTenant, ListTenants, GetTenant
- [x] TenantStore + seed (`public`, `dev`)
- [x] Middleware: `X-Tenant` / `X-Tenant-Id` / dev fallback

**Host Hardening**

- [x] JWT + CORS
- [x] Serilog request logging
- [x] Health checks (`/health/live`, `/health/ready`)
- [x] OpenTelemetry (opt-in via config)
- [x] Scalar + OpenAPI (Development)

**SOLID refinements**

- [x] `{Module}Module.cs` self-registration (OCP)
- [x] `AuthContracts` / `TenantContracts`
- [x] EF tenant query filters via `ITenantQueryFilterConfigurator`
- [x] `TenantContextBehavior` + `ITenantExemptRequest`
- [x] Architecture test: `Infrastructure` não referencia `Features`

---

## M3 — AI + E2E + DevOps ✅

**Goal:** Paridade AI + testes HTTP + pipeline

### Delivered

**AI Module**

- [x] AgentLoop, ToolRegistry, StubLlmService
- [x] Tools via MediatR: GetUsersSummary, GetTenantInfo
- [x] ChatAi slice (Command/Handler/Endpoint) + `FeatureFlags:EnableAI`
- [x] Policy `Authenticated` + tool authorization

**E2E Tests**

- [x] `tests/E2ETests` — Identity auth + Health HTTP

**Docker + CI/CD**

- [x] Dockerfile multi-stage Alpine + HEALTHCHECK
- [x] docker-compose (postgres + api)
- [x] GitHub Actions: build + tests + Trivy HIGH/CRITICAL gate

---

## M4 — Polish (optional)

- Getting started greenfield (`docs/guides/getting-started.md`)
- (Opcional) Azure OpenAI real, E2E HTTP expandido, harness `.agents/`

---

## Future Considerations

- Class Library por módulo + MSBuild deploy seletivo
- HostDbContext separado para tenants
- UpdateTenant / DeactivateTenant
- Rate limiting
