# AGENTS — Ai

Chat agent com tools; gated por feature flag. Agentes persistidos no Postgres do tenant.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `Agent.cs` | Agent + `IAgentRepository` + EF config (`AiAgents`) |
| `AgentFile.cs` | AgentFile + `IAgentFileRepository` + EF config (`AiAgentFiles`) |
| `AiUsageEntry.cs` | Ledger append-only de uso (`AiUsageEntries`) + `IAiUsageRepository` + `AiUsageTracker` |
| `ModelComparison.cs` | ModelComparison (owns `ModelComparisonResult`) + `IModelComparisonRepository` + EF config (`AiModelComparisons`, `AiModelComparisonResults`) |

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
| ListModels | `GET /api/v1/ai/models` | `AiAgentsRead` | `EnableAI` |
| CompareModels | `POST /api/v1/ai/comparisons` | `AiAgentsManage` | `EnableAI` |
| ListModelComparisons | `GET /api/v1/ai/comparisons` | `AiAgentsRead` | `EnableAI` |
| GetModelComparison | `GET /api/v1/ai/comparisons/{comparisonId}` | `AiAgentsRead` | `EnableAI` |

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
| `ContentGuard.cs` | `IContentGuard` (default `AllowAllContentGuard`), `GuardrailOptions`, `AgentGuardrails` (sufixo, delimitador, erros de tool) |
| `AiRateLimit.cs` | Policy `ai` (`IRateLimiterPolicy`, partição por tenant), `AiQuota` (tokens/dia), opções |
| `ModelCatalog.cs` | `IModelCatalog`: OpenRouter `/models` (só com `tools`, cache 1h), `ConfiguredModelCatalog` (MAF), `StubModelCatalog` |

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

- Modelo por agente: `Agent.Model` nulo = `Ai:Llm:Model`. `PUT /agents/{id}` substitui tudo — omitir `model` repõe o default
- MAF prende o cliente a `Ai:Llm:Model` na construção: ignora `LlmRequest.Model`, o catálogo só oferece os modelos configurados, e `POST /comparisons` responde `409`
- Uso já não é no-op: cada chat e cada modelo de uma comparação grava um `AiUsageEntry` (metadados, nunca conteúdo); falha a gravar só faz `LogError`
- Comparação corre o agente uma vez por modelo, em paralelo, num scope DI próprio: **tools correm N vezes** — uma tool com efeito colateral não pode entrar num agente comparado
- Guardrails no `AgentLoop` (valem para chat e comparação): system prompt = instruções + `AgentGuardrails.SystemSuffix`; toda a saída de tool é truncada, passa pelo `IContentGuard` e vai delimitada em `<tool_output>`; uma tool que lança vira `{"error":"permission_denied"|"tool_failed",...}` para o modelo — nunca `401`/`500`
- `history` do chat só aceita texto `user`/`assistant` (≤ 50, ≤ 4000 chars); `tool`/`system`/tool calls → `400`. Temporário até W2 (`conversas-agente`)
- Chat e comparações: `.RequireRateLimiting(RateLimitPolicies.AiRateLimitPolicy)` + `429` declarado; quota via `AiQuota.EnsureWithinAsync` antes do guard
- Testes de quota por HTTP precisam de ledger isolado: a InMemory do host é `AppDb` para o processo inteiro (`AiRateLimitTests.IsolatedLedgerFactory`)
- Catálogo inacessível → `ServiceUnavailableException` → `503`

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
  AgentModelTests.cs
  AgentLoopUsageTests.cs
  AiUsageTests.cs
  ListModelsTests.cs
  CompareModelsTests.cs
  AgentLoopGuardrailTests.cs
  AiRateLimitTests.cs
  AiTestDoubles.cs
```
