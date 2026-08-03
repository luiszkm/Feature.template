# AGENTS.md — Product.Template v2.1

**Template greenfield** — ponto de partida para produtos novos. Não há migração a partir do v1; o v1 serve só como referência funcional opcional. Ver `docs/guides/getting-started.md`.

Stack: .NET 10, EF Core, MediatR, FluentValidation, FeatureManagement, xUnit, VSA.

## Layout

```
src/App/
├── Program.cs          # ≤40 lines — não editar por slice
├── Host/               # configs, middleware, RequireFeature()
├── Shared/             # Kernel | Infrastructure | Platform
└── Features/{Module}/
    ├── {Entity}.cs     # domínio (substantivo)
    └── {Slice}.cs      # caso de uso (verbo)

tests/App.Tests/{Module}/{Slice}Tests.cs
features.json           # app + test + featureFlag
```

## Feature flags

- Config: `appsettings.json` → `FeatureFlags`
- Gate: `.RequireFeature(FeatureFlags.EnableAI)` no endpoint
- Constantes: `App.Host.FeatureFlags`

## Nova feature

1. `Features/{Module}/{Slice}.cs` — Command + Validator + Handler + IEndpoint
2. `tests/App.Tests/{Module}/{Slice}Tests.cs`
3. Atualizar `features.json`
4. **Não editar** `Program.cs`

## Regras

- Slice = arquivo plano; substantivo = entidade; verbo = slice
- Zero pastas por camada; zero testes em `src/App/`
- Repositórios: interface por agregado em `{Entity}.cs`; CRUD repetido via `Shared/EfRepositoryHelpers.cs` (`internal`) — sem `IRepository<T>` genérico
- Skill: `/new-slice {Module} {Slice}`
- Rule: `.cursor/rules/architecture-vsa.mdc`

## Module registration

Each module: `{Module}Module.cs` → `Add{Module}Module()` + optional `Add{Module}ModulePolicies()`.  
Orchestration: `AddFeatureModules()` in Host (Program.cs).

## Verificação

```bash
make verify
# ou:
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/App.Tests
dotnet test tests/E2ETests
```

## Docker + CI

```bash
# Subir stack completa (postgres + api)
docker compose up -d

# Build da imagem
docker build -t product-template-v2 .
```

CI: `.github/workflows/ci.yml` — build, ArchitectureTests, App.Tests, E2ETests, Trivy HIGH/CRITICAL.

## PostgreSQL (runtime)

Tests continuam em **InMemory** via `TestServiceFactory`. A API usa PostgreSQL quando `ConnectionStrings:Default` está configurada.

```bash
# 1. Subir Postgres
cp compose.env.example compose.env   # ajuste POSTGRES_PASSWORD
docker compose up -d postgres

# 2. Rodar API (aplica migrations + seed no startup)
cd src/App && dotnet run
```

Connection string dev (docker): `Host=localhost;Port=5432;Database=ProductTemplateV2;Username=postgres;Password=<POSTGRES_PASSWORD>`

Override de senha do admin seed: `dotnet user-secrets set "Seed:AdminPassword" "YourPassword"` (senão usa `Admin@123` só em Development).

Credenciais seed (dev): `admin@producttemplate.com` / `Admin@123` — tenant `dev` (`X-Tenant: dev`).

Health (com API rodando): `GET /health/live`

## Índice de slices

Ver `features.json`.
