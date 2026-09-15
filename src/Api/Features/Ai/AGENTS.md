# AGENTS — Ai

Chat agent com tools; gated por feature flag.

## Slices (verbo)

| Slice | Rota | Flag |
|-------|------|------|
| ChatAi | `POST /api/v1/ai/chat` | `EnableAI` |

Ver `features.json` com `"m": "Ai"`.

## Infra do módulo

| Ficheiro | Papel |
|----------|-------|
| `AiModule.cs` | DI: AgentLoop, ToolRegistry, StubLlmService |
| `AiContracts.cs` | Request/response DTOs |
| `AgentLoop.cs` | Orquestração LLM + tools |
| `ToolRegistry.cs` | Registo de tools |
| `ToolAuthorization.cs` | Autorização por tool |
| `AgentSystemPrompt.cs` | System prompt |

## Tools

| Tool | Dep |
|------|-----|
| `GetUsersSummaryTool` | `IUserDirectory` (Shared; Identity implementa) |
| `GetTenantInfoTool` | `ITenantDirectory` (Shared; Tenants implementa) |

## Feature flag

- Config: `FeatureFlags:EnableAI` em `appsettings.json`
- Gate: `.RequireFeature(FeatureFlags.EnableAI)` no endpoint (`Api.Shared`)
- Policy: `Authenticated`

## Gotchas

- Produção: `StubLlmService` — substituir por implementação real (Azure OpenAI, etc.)
- Sem agregado EF próprio neste módulo
- Tools acedem a outros módulos via contratos de leitura em Shared (`IUserDirectory`, `ITenantDirectory`), não via MediatR nem tipos de Identity/Tenants
- Permissões das tools: `DirectoryPermissions.UsersRead` / `TenantsRead` (Shared) — não importar `IdentityPermissions` / `TenantsPermissions`

## Testes

```
tests/Api.Tests/Ai/
  ChatAiHandlerTests.cs
```
