# Vertical Slice Architecture (v2.1)

Product.Template usa **VSA** dentro de um único projeto `App` (Sdk.Web): host HTTP + aplicação unificados.

## Layout

```
src/App/
├── Program.cs              # ≤40 linhas — não editar por slice
├── Host/                   # configs, middleware, seeders, segurança
├── Shared/                 # Kernel, Infrastructure, Platform (cross-cutting global)
└── Features/{Module}/
    ├── AGENTS.md           # contexto do módulo (ler antes de editar)
    ├── {Entity}.cs         # substantivo: domínio + repo + EF config
    ├── {Module}Module.cs   # DI + policies (quando RBAC)
    └── {Slice}.cs          # verbo: Command/Query + Validator + Handler + IEndpoint

tests/App.Tests/{Module}/   # espelho plano (sem Features/ prefix)
tests/ArchitectureTests/  # fronteiras namespace/pasta
tests/E2ETests/             # contratos HTTP
features.json               # índice app + test + route + policy + featureFlag
```

## Princípios

1. **Slice = ficheiro plano** — um caso de uso por ficheiro (`RegisterUser.cs`), não pastas por camada.
2. **Substantivo vs verbo** — `User.cs` (entidade) vs `RegisterUser.cs` (caso de uso).
3. **Auto-discovery** — endpoints via `IEndpoint` + `MapEndpointsFromAssembly()`; **não** editar `Program.cs` por slice.
4. **Testes separados** — padrão .NET; espelho plano em `tests/App.Tests/{Module}/`.
5. **Shared não referencia Features** — regra enforced em ArchitectureTests.

## Host

| Área | Path |
|------|------|
| Registo de módulos | `Host/Configurations/FeatureModulesConfiguration.cs` |
| Segurança JWT / policies | `Host/Configurations/SecurityConfiguration.cs` |
| Feature flags | `Host/FeatureFlags.cs` + `appsettings.json` |
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
- Constantes: `App.Host.FeatureFlags`
- Gate no endpoint: `.RequireFeature(FeatureFlags.EnableAI)`

## Harness AGENTS (hierarquia)

```
AGENTS.md (raiz)           → regras cross-cutting do template
Features/{Module}/AGENTS.md → contexto do bounded context
docs/architecture/         → arquitetura e guidelines (este doc)
features.json              → índice de slices
```

**Regra:** ao editar ficheiros em `Features/{Module}/`, ler `Features/{Module}/AGENTS.md` primeiro.

Novos módulos devem incluir `AGENTS.md` (skill `/new-module`).

## Verificação

```bash
make verify
# ou:
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/App.Tests
dotnet test tests/E2ETests
```
