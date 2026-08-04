# AGENTS.md — Product.Template v2.1

**Template greenfield** — ponto de partida para produtos novos. Ver `docs/guides/getting-started.md`.

Stack: .NET 10, EF Core, MediatR, FluentValidation, FeatureManagement, xUnit, VSA.

## Regra de contexto

Ao editar ficheiros em `Features/{Module}/`, **ler primeiro** `src/App/Features/{Module}/AGENTS.md`.

## Layout

```
src/App/
├── Program.cs          # ≤40 lines — não editar por slice
├── Host/               # configs, middleware, RequireFeature()
├── Shared/             # Kernel | Infrastructure | Platform
└── Features/{Module}/
    ├── AGENTS.md       # contexto do módulo
    ├── {Entity}.cs     # domínio (substantivo)
    └── {Slice}.cs      # caso de uso (verbo)

tests/App.Tests/{Module}/{Slice}Tests.cs
features.json           # app + test + route + policy + featureFlag
```

## Módulos

| Módulo | AGENTS |
|--------|--------|
| Identity | [Features/Identity/AGENTS.md](src/App/Features/Identity/AGENTS.md) |
| Authorization | [Features/Authorization/AGENTS.md](src/App/Features/Authorization/AGENTS.md) |
| Tenants | [Features/Tenants/AGENTS.md](src/App/Features/Tenants/AGENTS.md) |
| Ai | [Features/Ai/AGENTS.md](src/App/Features/Ai/AGENTS.md) |

## Docs

- [docs/architecture/vsa.md](docs/architecture/vsa.md) — arquitetura
- [docs/architecture/guidelines.md](docs/architecture/guidelines.md) — padrões de código
- [docs/security/RBAC_MATRIX.md](docs/security/RBAC_MATRIX.md) — políticas

## Nova feature

1. Ler `Features/{Module}/AGENTS.md`
2. `Features/{Module}/{Slice}.cs` — Command + Validator + Handler + IEndpoint
3. `tests/App.Tests/{Module}/{Slice}Tests.cs`
4. Atualizar `features.json`
5. **Não editar** `Program.cs`

Skills: `/new-module {Module}` · `/new-slice {Module} {Slice}` · `/vsa-review`  
Rules: `.cursor/rules/architecture-vsa.mdc` · `.cursor/rules/agent-boundaries.mdc`

## Regras globais

- Slice = arquivo plano; substantivo = entidade; verbo = slice
- Zero pastas por camada; zero testes em `src/App/`
- Repo por agregado em `{Entity}.cs`; sem `IRepository<T>` genérico
- Module: `{Module}Module.cs` → `AddFeatureModules()` in Host

## Verificação

```bash
make verify
```

## Commits

Conventional Commits: `feat(module):`, `fix(module):`, `test:`, `docs:`, `chore:` — só commitar quando pedido.

## Plan → Execute → Verify

- **Plan Mode:** multi-módulo, auth/tenancy/security, migrations
- **Agent Mode:** slice scoped com critérios claros
- Após falha: no máximo 2 ciclos fix→retest

## Docker + CI

```bash
docker compose up -d          # postgres + api
docker build -t product-template-v2 .
```

CI: `.github/workflows/ci.yml` — build, ArchitectureTests, App.Tests, E2ETests.

## PostgreSQL (runtime)

Tests: InMemory via `TestServiceFactory`. API: PostgreSQL com `ConnectionStrings:Default`.

```bash
cp compose.env.example compose.env
docker compose up -d postgres
cd src/App && dotnet run
```

Credenciais dev e tenant header: `docs/guides/getting-started.md`.

## Índice de slices

Ver `features.json`.
