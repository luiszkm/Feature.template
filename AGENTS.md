# AGENTS.md — Product.Template v2.1

**Template greenfield** — ponto de partida para produtos novos. Ver `docs/guides/getting-started.md`.

Stack: .NET 10, EF Core, MediatR, FluentValidation, FeatureManagement, xUnit, VSA.
Front: Angular 22 (zoneless, Signal Forms), Angular Material, Vitest + MSW, Playwright — `src/web/`.

## tlc-spec-lean

profile: ui
budget: 150k

## Regra de contexto

Ao editar ficheiros em `Features/{Module}/`, **ler primeiro** `src/Api/Features/{Module}/AGENTS.md`.

## Layout

```
src/Api/
├── Program.cs          # ≤40 lines — não editar por slice
├── Host/               # configs, middleware, seeders
├── Shared/             # Kernel | Infrastructure | Platform (policies, flags, RequireFeature)
└── Features/{Module}/
    ├── AGENTS.md       # contexto do módulo
    ├── {Entity}.cs     # domínio (substantivo)
    └── {Slice}.cs      # caso de uso (verbo)

tests/Api.Tests/{Module}/{Slice}Tests.cs
features.json           # app + test + route + policy + featureFlag
src/Api/openapi.json    # contrato versionado (regenerar ao mudar endpoint)
src/web/                # front Angular — VSA espelhada
```

## Módulos

| Módulo | AGENTS |
|--------|--------|
| Identity | [Features/Identity/AGENTS.md](src/Api/Features/Identity/AGENTS.md) |
| Authorization | [Features/Authorization/AGENTS.md](src/Api/Features/Authorization/AGENTS.md) |
| Tenants | [Features/Tenants/AGENTS.md](src/Api/Features/Tenants/AGENTS.md) |
| Ai | [Features/Ai/AGENTS.md](src/Api/Features/Ai/AGENTS.md) |
| Web (front) | [src/web/AGENTS.md](src/web/AGENTS.md) |

## Docs

- [docs/architecture/vsa.md](docs/architecture/vsa.md) — arquitetura
- [docs/architecture/guidelines.md](docs/architecture/guidelines.md) — padrões de código
- [docs/security/RBAC_MATRIX.md](docs/security/RBAC_MATRIX.md) — políticas

## Nova feature

1. Ler `Features/{Module}/AGENTS.md`
2. `Features/{Module}/{Slice}.cs` — Command + Validator + Handler + IEndpoint
3. `tests/Api.Tests/{Module}/{Slice}Tests.cs`
4. Atualizar `features.json`
5. `UPDATE_OPENAPI=1 dotnet test tests/E2ETests` — regenera o contrato
6. Cliente no front em `src/web/src/app/features/{module}/` — `npm test` falha sem ele
7. **Não editar** `Program.cs`

Skills: `/new-module {Module}` · `/new-slice {Module} {Slice}` · `/vsa-review`  
Rules: `.cursor/rules/architecture-vsa.mdc` · `.cursor/rules/agent-boundaries.mdc`

## Regras globais

- Slice = arquivo plano; substantivo = entidade; verbo = slice
- Zero pastas por camada; zero testes em `src/Api/`
- Repo por agregado em `{Entity}.cs`; sem `IRepository<T>` genérico
- Module: `{Module}Module.cs` → `AddFeatureModules()` in Host

## Front-end

```
src/web/src/app/
├── core/ shared/ shell/        # sessão, HTTP, estados de lista, navegação
└── features/{module}/{slice}.ts   # store + cliente + componente, ficheiro plano
```

Mesma regra de VSA: zero pastas de camada em `features/`. Toda a rota de `features.json` tem de
ter cliente em `src/web/src/app/**` — `npm test` falha se faltar. Contexto: `src/web/AGENTS.md`.

## Contrato

`src/Api/openapi.json` é versionado e é a autoridade. Mudou um endpoint?
`UPDATE_OPENAPI=1 dotnet test tests/E2ETests` regenera; `tests/ArchitectureTests` falha se
`features.json` e o documento divergirem, nos dois sentidos.

## Verificação

```bash
make verify-all    # tudo o que um PR toca: API + guardas de contrato + front
make verify        # backend: build + ArchitectureTests + Api.Tests + E2ETests
make web-verify    # front: vitest + build
make web-e2e       # front: playwright contra a API real (docker compose up)
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

CI: `.github/workflows/ci.yml` — build, ArchitectureTests, Api.Tests, E2ETests.

## PostgreSQL (runtime)

Tests: InMemory via `TestServiceFactory`. API: PostgreSQL com `ConnectionStrings:Default`.

```bash
cp compose.env.example compose.env
docker compose up -d postgres
cd src/Api && dotnet run
```

Credenciais dev e tenant header: `docs/guides/getting-started.md`.

## Índice de slices

Ver `features.json`.
