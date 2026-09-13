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
| `GetUsersSummaryTool` | Identity (`IUserRepository`) |
| `GetTenantInfoTool` | Tenants |

## Feature flag

- Config: `FeatureFlags:EnableAI` em `appsettings.json`
- Gate: `.RequireFeature(FeatureFlags.EnableAI)` no endpoint
- Policy: `Authenticated`

## Gotchas

- Produção: `StubLlmService` — substituir por implementação real (Azure OpenAI, etc.)
- Sem agregado EF próprio neste módulo
- Tools acedem a outros módulos via interfaces públicas apenas

## Testes

```
tests/App.Tests/Ai/
  ChatAiHandlerTests.cs
```
