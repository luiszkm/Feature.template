# Vertical Slice Architecture (v2.1)

Product.Template usa **VSA** dentro de um único projeto `Api` (Sdk.Web): host HTTP + aplicação unificados.

## Layout

```
src/Api/
├── Program.cs              # ≤40 linhas — não editar por slice
├── Host/                   # configs, middleware, seeders, segurança
├── Shared/                 # Kernel, Infrastructure, Platform (cross-cutting global)
└── Features/{Module}/
    ├── AGENTS.md           # contexto do módulo (ler antes de editar)
    ├── {Entity}.cs         # substantivo: domínio + repo + EF config
    ├── {Module}Module.cs   # DI + policies (quando RBAC)
    └── {Slice}.cs          # verbo: Command/Query + Validator + Handler + IEndpoint

tests/Api.Tests/{Module}/   # espelho plano (sem Features/ prefix)
tests/ArchitectureTests/  # fronteiras namespace/pasta
tests/E2ETests/             # contratos HTTP
features.json               # índice app + test + route + policy + featureFlag
```

## Princípios

1. **Slice = ficheiro plano** — um caso de uso por ficheiro (`RegisterUser.cs`), não pastas por camada.
2. **Substantivo vs verbo** — `User.cs` (entidade) vs `RegisterUser.cs` (caso de uso).
3. **Auto-discovery** — endpoints via `IEndpoint` + `MapEndpointsFromAssembly()`; **não** editar `Program.cs` por slice.
4. **Testes separados** — padrão .NET; espelho plano em `tests/Api.Tests/{Module}/`.
5. **Shared não referencia Features** — regra enforced em ArchitectureTests.
6. **Features.{A} não referencia Features.{B}** — contratos cross-module em Shared (`IUserRolesProvider`, `IUserLookup`, `ISecurityStampService`, `IUserDirectory`, `ITenantDirectory`). Allowlist de ArchitectureTests deve permanecer vazia.
7. **Features não referencia Host** — rate limit, policies, flags e `RequireFeature` vivem em Shared; Host só compõe.

## Host

| Área | Path |
|------|------|
| Registo de módulos | `Host/Configurations/FeatureModulesConfiguration.cs` |
| Nomes de policies | `Shared/SecurityPolicies.cs` |
| Segurança JWT | `Host/Configurations/SecurityConfiguration.cs` + `Host/Security/` |
| Feature flags | `Shared/FeatureFlags.cs` + `appsettings.json` (`Host` regista FeatureManagement) |
| Seed dev | `Host/Seeders/` |

## Módulos atuais

| Módulo | Registo | Policies |
|--------|---------|----------|
| Identity | `IdentityModule.cs` | UsersRead, UsersManage, UserReadOrSelf, UserManageOrSelf |
| Authorization | `AuthorizationModule.cs` | AuthorizationRoles*, AuthorizationPermissions* |
| Tenants | `TenantsModule.cs` | TenantsRead, TenantsManage |
| Ai | `AiModule.cs` | Authenticated + `EnableAI` feature flag |

## Feature flags

- Config: `appsettings.json` → `FeatureFlags`
- Constantes: `Api.Shared.FeatureFlags`
- Gate no endpoint: `.RequireFeature(FeatureFlags.EnableAI)`

## Harness AGENTS (hierarquia)

```
AGENTS.md (raiz)           → regras cross-cutting do template
Features/{Module}/AGENTS.md → contexto do bounded context
src/web/AGENTS.md          → contexto do front (VSA espelhada em Angular)
docs/architecture/         → arquitetura e guidelines (este doc)
features.json              → índice de slices
src/Api/openapi.json       → contrato versionado, autoridade partilhada
```

**Regra:** ao editar ficheiros em `Features/{Module}/`, ler `Features/{Module}/AGENTS.md` primeiro.

Novos módulos devem incluir `AGENTS.md` (skill `/new-module`).

## Contrato

`src/Api/openapi.json` é versionado e é a autoridade entre API, `features.json` e o front. Quatro
guardas mantêm os três alinhados:

| Guarda | Onde | Falha quando |
|--------|------|--------------|
| snapshot | `OpenApiDocumentTests` | o endpoint mudou e o documento não foi regenerado |
| `features.json` → doc | `OpenApiContractTests` | rota indexada que a API não expõe |
| doc → `features.json` | `OpenApiContractTests` | rota exposta que ninguém indexou |
| clientes → doc | `src/web` `architecture.spec.ts` | o front chama um caminho que não existe |

Regenerar: `UPDATE_OPENAPI=1 dotnet test tests/E2ETests`.

## Verificação

```bash
make verify        # backend: build + ArchitectureTests + Api.Tests + E2ETests
make web-verify    # front: vitest + build
make web-e2e       # front: playwright contra a API real
```
