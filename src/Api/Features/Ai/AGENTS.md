# AGENTS — Ai

Chat agent com tools; gated por feature flag. Agentes persistidos no Postgres do tenant.

## Agregados (substantivo)

| Ficheiro | Conteúdo |
|----------|----------|
| `Agent.cs` | Agent + `IAgentRepository` + EF config (`AiAgents`) |
| `AgentFile.cs` | AgentFile + `IAgentFileRepository` + EF config (`AiAgentFiles`) |
| `AiUsageEntry.cs` | Ledger append-only de uso (`AiUsageEntries`) + `IAiUsageRepository` + `AiUsageTracker` |
| `Conversation.cs` | Conversation (agregado, `TenantId` + `UserId` + `AgentId`, `Items`) + `IConversationRepository` (posse por linha) + EF config (`AiConversations`) |
| `ConversationItem.cs` | ConversationItem (`Sequence`, `Role`, `Content`) + EF config (`AiConversationItems`, único `(ConversationId, Sequence)`, cascade) |
| `ModelComparison.cs` | ModelComparison (owns `ModelComparisonResult`) + `IModelComparisonRepository` + EF config (`AiModelComparisons`, `AiModelComparisonResults`) |

## Slices (verbo)

| Slice | Rota | Policy | Flag |
|-------|------|--------|------|
| ChatAi | `POST /api/v1/ai/chat` | `Authenticated` | `EnableAI` |
| GetAiUsage | `GET /api/v1/ai/usage` | `AiAgentsRead` | `EnableAI` |
| ListConversations | `GET /api/v1/ai/conversations` | `Authenticated` | `EnableAI` |
| GetConversation | `GET /api/v1/ai/conversations/{conversationId}` | `Authenticated` | `EnableAI` |
| DeleteConversation | `DELETE /api/v1/ai/conversations/{conversationId}` | `Authenticated` | `EnableAI` |
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
| `AiTelemetry.cs` | `ActivitySource` `Api.Features.Ai` e vocabulário GenAI (`invoke_agent`, `chat`, `execute_tool`); registado em `Host/Configurations/ObservabilityConfiguration.cs` |
| `ContentGuard.cs` | `IContentGuard` (default `AllowAllContentGuard`), `GuardrailOptions`, `AgentGuardrails` (sufixo, delimitador, erros de tool) |
| `ConversationRetentionService.cs` | `BackgroundService`: apaga conversas com `LastActivityAt` anterior a `Ai:Conversations:RetentionDays` (90; `0` desliga), a cada `PurgeIntervalHours` (24), `IgnoreQueryFilters` |
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
- O servidor é dono do transcript: `POST /ai/chat` recusa `history` (`400`, chave `history`), cria a conversa sem `conversationId` e devolve-o; o histórico reenviado são os últimos `HistoryWindow` (20) itens `user`/`assistant` não vazios — os `tool` ficam gravados e não são reenviados
- `AgentLoop`: a pergunta vai em `UserPrompt` só na primeira chamada; depois de uma tool entra no histórico como `user` antes do `assistant` que pediu a tool, e fica fora de `TurnMessages`. Cada pedido leva uma cópia do histórico, nunca a lista que o loop continua a mutar
- Um turno grava user + mensagens do loop + resposta num só `SaveChangesAsync`; nada é gravado se o loop lança, o guard bloqueia ou a quota recusa. `LastActivityAt` é concurrency token: dois appends simultâneos → o segundo `409`
- Posse no `IConversationRepository` (`UserId` do JWT), não numa policy; o chat exige utilizador — testes de handler chamam `TestServiceFactory.SetUser`
- Agente fixado na conversa: outro `agentId` → `409`; agente desactivado → `404`; `MaxItems` (200) → `409`
- Chat e comparações: `.RequireRateLimiting(RateLimitPolicies.AiRateLimitPolicy)` + `429` declarado; quota via `AiQuota.EnsureWithinAsync` antes do guard
- Testes de quota por HTTP precisam de ledger isolado: a InMemory do host é `AppDb` para o processo inteiro (`AiRateLimitTests.IsolatedLedgerFactory`)
- Spans GenAI: `invoke_agent {agente}` no `ChatAiHandler` (com `gen_ai.conversation.id`), `chat {modelo}` por chamada ao LLM e `execute_tool {tool}` no `AgentLoop`; nunca conteúdo (mensagens, argumentos, resultados). Só existem com `OpenTelemetry:EnableTraces=true` — e o flag é lido no registo de serviços, por isso nos testes liga-se com `UseSetting`, não com settings em memória
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
  AgentTelemetryTests.cs
  GetAiUsageTests.cs
  ListConversationsTests.cs
  GetConversationTests.cs
  DeleteConversationTests.cs
  ConversationRetentionServiceTests.cs
  ConversationTestSupport.cs / ConversationHttp.cs
  AiTestDoubles.cs
```
