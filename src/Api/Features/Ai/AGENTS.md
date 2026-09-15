# AGENTS — Ai

Chat agent com tools; gated por feature flag. Agentes persistidos no Postgres do tenant.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `Agent.cs` | Agent + `IAgentRepository` + EF config (`AiAgents`) |
| `AgentFile.cs` | AgentFile + `IAgentFileRepository` + EF config (`AiAgentFiles`) |

## Slices (verbo)

| Slice | Rota | Policy | Flag |
|-------|------|--------|------|
| ChatAi | `POST /api/v1/ai/chat` | `Authenticated` | `EnableAI` |
| CreateAgent | `POST /api/v1/ai/agents` | `AiAgentsManage` | `EnableAI` |
| ListAgents | `GET /api/v1/ai/agents` | `AiAgentsRead` | `EnableAI` |
| GetAgent | `GET /api/v1/ai/agents/{agentId}` | `AiAgentsRead` | `EnableAI` |
| UpdateAgent | `PUT /api/v1/ai/agents/{agentId}` | `AiAgentsManage` | `EnableAI` |
| DeactivateAgent | `DELETE /api/v1/ai/agents/{agentId}` | `AiAgentsManage` | `EnableAI` |
| CreateAgentFile | `POST /api/v1/ai/agents/{agentId}/files` | `AiAgentsManage` | `EnableAI` |
| ListAgentFiles | `GET /api/v1/ai/agents/{agentId}/files` | `AiAgentsRead` | `EnableAI` |
| GetAgentFile | `GET /api/v1/ai/agents/{agentId}/files/{fileId}` | `AiAgentsRead` | `EnableAI` |
| DeleteAgentFile | `DELETE /api/v1/ai/agents/{agentId}/files/{fileId}` | `AiAgentsManage` | `EnableAI` |

Ver `features.json` com `"m": "Ai"`.

## Infra do módulo

| Ficheiro | Papel |
|----------|-------|
| `AiModule.cs` | DI, policies, query filters, `ILlmService` switch |
| `AiContracts.cs` | `ILlmService`, tools, usage |
| `AgentContracts.cs` | DTOs + `LlmOptions` + `LlmProviders` |
| `AgentLoop.cs` | Orquestração LLM + tools da allowlist |
| `ToolRegistry.cs` | Registo de tools + allowlist |
| `StubLlmService.cs` | Testing / Development sem chave |
| `OpenRouterLlmService.cs` | Provider `OpenRouter` |
| `MicrosoftAgentFrameworkLlmService.cs` | Provider `MicrosoftAgentFramework` |
| `AgentSystemPrompt.cs` | Texto do seed default |

## Tools

| Tool | Dep |
|------|-----|
| `GetUsersSummaryTool` | `IUserDirectory` (Shared; Identity implementa) |
| `GetTenantInfoTool` | `ITenantDirectory` (Shared; Tenants implementa) |
| `ListAgentFilesTool` / `ReadAgentFileTool` | `IAgentFileRepository` + `IAgentRuntimeContext` |

## Feature flag

- Config: `FeatureFlags:EnableAI` em `appsettings.json`
- Gate: `.RequireFeature(FeatureFlags.EnableAI)` no endpoint (`Api.Shared`)

## LLM

- `Ai:Llm:Provider` = `OpenRouter` (default) ou `MicrosoftAgentFramework`
- Chave: `Ai:Llm:ApiKey` / env `AI_LLM_API_KEY` — placeholder em `compose.env.example`, valor em `compose.env` (gitignored) ou user-secrets
- Testing, ou Development sem chave → `StubLlmService`
- Production + `EnableAI=true` sem chave → fail-fast (`InvalidOperationException` nomeia `Ai:Llm:ApiKey`)

## Gotchas

- Features ↛ Features/Host: seed em `CreateTenant` via `IDefaultAgentProvisioner` (Shared)
- Soft-delete: `DeactivateAgent`; o último activo do tenant recusa com 409
- Chat sem `agentId` usa o seed (`IsDefault`); id desconhecido/inactivo → 404, sem fallback
- Tools acedem a outros módulos via contratos Shared, não via MediatR

## Testes

```
tests/Api.Tests/Ai/
  CreateAgentTests.cs
  ChatAiHandlerTests.cs
  ChatAiTests.cs
  CreateAgentFileTests.cs
  LlmServiceTests.cs
```
