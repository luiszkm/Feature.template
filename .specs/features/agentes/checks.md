# Agentes checks

Profile: ui
Plan: `.specs/features/agentes/plan.md`

## Intent

40 acceptance criteria in 5 slices · 7 one-way doors · 0 open

## Checks

Grouped by the plan's slices; numbering runs across the whole feature. Commands from the repo root.

### S1 - Segundo agente no tenant · AGENT-01 · ~18k

**C1** - `POST /api/v1/ai/agents` with `ai.agent.manage`, `name`, `instructions` and known `toolNames` persists an `Agent` in the current tenant and returns `201` with `agentId`, `name`, `instructions`, `toolNames`, `isActive: true` (AGENT-01, AC 1)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Handle_ShouldCreateAgent_WhenInputIsValid`

**C2** - `GET /api/v1/ai/agents` with `AiAgentsRead` returns page `pageNumber` default `1`, `pageSize` default `20`, only current-tenant agents, ordered by `createdAt` desc then `Id` when `sortBy` is omitted (AGENT-01, AC 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListAgentsTests.Handle_ShouldReturnTenantAgents_OrderedByCreatedAtDesc`

**C3** - Repeating `POST /api/v1/ai/agents` with a `name` already used in the same tenant returns `409` with ProblemDetails title `Business rule violation` and does not create a second row (AGENT-01, AC 3)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Handle_ShouldThrow_WhenNameAlreadyExists`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Post_ShouldReturn409_WhenNameIsDuplicate`

**C4** - `PUT /api/v1/ai/agents/{agentId}` mutates the same row (`name`, `instructions`, `toolNames`) and returns `200` without creating a version resource (AGENT-01, AC 4)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~UpdateAgentTests.Handle_ShouldMutateSameRow_WhenInputIsValid`

**C5** - `DELETE /api/v1/ai/agents/{agentId}` on an agent that is not the last active in the tenant returns `204` and a later `GET` of that id returns `isActive: false` (AGENT-01, AC 5)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeactivateAgentTests.Handle_ShouldDeactivate_WhenNotLastActive`

**C6** - `GET`/`PUT`/`DELETE` of an `{agentId}` that does not exist in this tenant returns `404` with title `Not found` (AGENT-01, AC 6)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAgentTests.Handle_ShouldThrow_WhenAgentDoesNotExist`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~UpdateAgentTests.Handle_ShouldThrow_WhenAgentDoesNotExist`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeactivateAgentTests.Handle_ShouldThrow_WhenAgentDoesNotExist`

**C7** - `toolNames` containing a name unknown to `ToolRegistry` returns `400` with title `Validation failed` (AGENT-01, AC 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Validator_ShouldFail_WhenToolNameIsUnknown`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Post_ShouldReturn400_WhenToolNameIsUnknown`

**C8** - When `FeatureFlags:EnableAI` is `false`, every verb on `/api/v1/ai/agents` returns `404` with title `Feature disabled` (AGENT-01, AC 8)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Agents_ShouldReturn404_WhenEnableAiIsFalse`

**C9** - Authenticated caller without `ai.agent.read` gets `403` on `GET`; without `ai.agent.manage` gets `403` on `POST`/`PUT`/`DELETE` (AGENT-01, AC 9)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Get_ShouldReturn403_WhenCallerLacksReadPermission`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Post_ShouldReturn403_WhenCallerLacksManagePermission`

**C10** - Tenant `dev` at bootstrap, and a tenant created via `CreateTenant`, have exactly one active default agent whose `instructions` are `AgentSystemPrompt.Text` and whose tools are `get_users_summary` and `get_tenant_info` (AGENT-01, AC 10)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Seed_ShouldCreateDefaultAgent_ForDevTenant`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.CreateTenant_ShouldSeedDefaultAgent`

**C11** - `DELETE` of the last active agent in the tenant returns `409` and leaves the row active (AGENT-01, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeactivateAgentTests.Handle_ShouldThrow_WhenLastActiveAgent`

**C41** - Unauthenticated `GET /api/v1/ai/agents` returns `401` (AGENT-01, Surface)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentTests.Get_ShouldReturn401_WhenNotAuthenticated`

### S2 - Chat corre o agente pedido · AGENT-02 · ~8k

**C12** - `POST /api/v1/ai/chat` with `agentId` of an active tenant agent makes `AgentLoop` use that agent's `instructions` and only its allowlisted tools (AGENT-02, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldUseAgentAllowlist_WhenAgentIdIsProvided`

**C13** - Omitting `agentId` runs the tenant default (seed) and returns `200` with `reply` and `iterationsUsed` (AGENT-02, AC 13)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldReturnReply_WhenMessageIsValid`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200_WhenEnableAiIsTrueAndAuthenticated`

**C14** - Unknown `agentId` in this tenant, or `isActive` false, returns `404` with title `Not found` and does not fall through to the default (AGENT-02, AC 14)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenAgentIdIsUnknown`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenAgentIsInactive`

**C15** - A model-requested tool outside the agent's allowlist is not executed; `ToolRegistry` returns `{"error":"..."}` (or the same shape used today for an unknown name) and the loop continues (AGENT-02, AC 15)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldNotExecuteTool_OutsideAllowlist`

**C16** - `POST /api/v1/ai/chat` stays on policy `Authenticated` (not `AiAgentsManage`) (AGENT-02, AC 16)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn401_WhenNotAuthenticated`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200_WhenEnableAiIsTrueAndAuthenticated`

**C17** - `EnableAI` false returns `404` title `Feature disabled` on chat, same as CRUD (AGENT-02, AC 17)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn404_WhenEnableAiIsFalse`

**C18** - Chat still accepts `history` in the body and does not persist threads on the server (AGENT-02, AC 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldAcceptHistory_WithoutPersistingThreads`

### S3 - Admin configura na UI; utilizador escolhe no chat · AGENT-03 · ~22k

Binding screens: `agents-list` (Tenants analogue: header h1 + primary action right; `mat-form-field` Pesquisar; `app-list-state`; `mat-table` + `mat-sort` + `mat-paginator`), `agent-form`, `chat` (picker above history, full card width).

**C19** - `/ai/agents` with `ai.agent.read` and `totalCount` `0` shows empty copy `Nenhum agente` and action `Criar agente` pointing at `/ai/agents/new` (AGENT-03, AC 19)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agents-list.spec.ts --filter "estado vazio"`

**C20** - While the agents list is `loading`, the screen shows `mat-progress-bar` (`data-testid="list-loading"`) and the paginator is disabled (AGENT-03, AC 20)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de carregamento: agents"`

**C21** - Agents list `500` shows the ProblemDetails `title` and button `Tentar de novo` (AGENT-03, AC 21)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de erro repete a query: agents"`

**C22** - Confirming Desativar (`title` `Desativar agente`, `confirmLabel` `Desativar`) calls `DELETE /api/v1/ai/agents/{agentId}` and marks the row `isActive: false`; cancelling sends zero HTTP (AGENT-03, AC 22)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agents-list.spec.ts --filter "desativar com confirm"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agents-list.spec.ts --filter "desativar cancelado"`

**C23** - `/ai/agents/new` submit with name and instructions `POST`s `/api/v1/ai/agents` and on `201` navigates to `/ai/agents` (AGENT-03, AC 23)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agent-form.spec.ts --filter "201 navega para agents"`

**C24** - `POST`/`PUT` `400` `ValidationProblemDetails` shows each `errors[campo]` outside `<mat-error>` (AGENT-03, AC 24)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agent-form.spec.ts --filter "400 mostra errors fora de mat-error"`

**C25** - Opening `/ai/agents` without `ai.agent.read` renders `forbidden` with `Sem permissão para esta operação` (AGENT-03, AC 25)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agents-list.spec.ts --filter "sem permissao vai para forbidden"`

**C26** - When `AiAvailability` learned the flag is off (404 `Feature disabled`), nav links `AI` and `Agentes` are hidden (AGENT-03, AC 26)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "esconde AI e Agentes quando a flag esta off"`

**C27** - `/ai` shows an active-agent picker labelled `Agente`; `POST /api/v1/ai/chat` includes the selected `agentId`; picker default is the seed agent (AGENT-03, AC 27)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "envia agentId do picker"`

**C28** - Chat with no messages keeps copy `Faça uma pergunta`; while `pending` keeps `A escrever…` (AGENT-03, AC 28)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "envia e renderiza o reply"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "pendente desativa o envio"`

**C29** - Flat clients live in `src/web/src/app/features/ai/` (`agents-list.ts`, `agent-form.ts`, `chat.ts` sends `agentId`); zero layer folders (AGENT-03, AC 29)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "todas as rotas de features.json tem cliente"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "sem pastas de camada"`

**C42** - `agents-list` arrangement: header with h1 plus primary action on the right, then Pesquisar, then `app-list-state`, then `mat-table`/`mat-sort`, then `mat-paginator` (AGENT-03, Observable)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agents-list.spec.ts --filter "arranjo igual a tenants"`

**C43** - Chat picker sits above the history list, full width of the `mat-card` (AGENT-03, Observable)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "picker acima do historico"`

### S4 - Chat deixa de ser stub em runtime configurado · AGENT-04 · ~12k

**C30** - With `Ai:Llm:ApiKey` set, environment not `Testing`, and `Ai:Llm:Provider` `OpenRouter` or `MicrosoftAgentFramework`, `ILlmService.CompleteAsync` issues an HTTP request to that provider's `BaseUrl`, not `StubLlmService` (AGENT-04, AC 30)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~OpenRouterLlmServiceTests.CompleteAsync_ShouldPostChatCompletions_ToBaseUrl`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~MicrosoftAgentFrameworkLlmServiceTests.CompleteAsync_ShouldSendHttp_ToBaseUrl`

**C31** - Environment `Testing`, or empty key in Development, still registers `StubLlmService` (AGENT-04, AC 31)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceRegistrationTests.ShouldRegisterStub_WhenTesting`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceRegistrationTests.ShouldRegisterStub_WhenDevelopmentAndApiKeyEmpty`

**C32** - `EnableAI=true` in Production with empty API key fails host startup with `InvalidOperationException` whose message contains `Ai:Llm:ApiKey` (AGENT-04, AC 32)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceRegistrationTests.ShouldFailFast_WhenProductionEnableAiAndApiKeyEmpty`

**C33** - Chat on the real provider records `IAiUsageTracker` `Provider` and `Model` from config, not literals `stub`/`stub` (AGENT-04, AC 33)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldTrackConfiguredProviderAndModel`

**C34** - LLM HTTP failure is handled by the existing exception handler as `500` title `Unexpected error` (AGENT-04, AC 34)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn500_WhenLlmHttpFails`

### S5 - Ficheiros de conhecimento por agente · AGENT-05 · ~10k

**C35** - Manage `POST /api/v1/ai/agents/{agentId}/files` with `name` and `content` (text) persists an `AgentFile` for that agent and current tenant and returns `201` with `fileId`, `name` (AGENT-05, AC 35)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentFileTests.Handle_ShouldCreateFile_WhenInputIsValid`

**C36** - `GET /api/v1/ai/agents/{agentId}/files` lists only that agent's files in the current tenant (AGENT-05, AC 36)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListAgentFilesTests.Handle_ShouldListOnlyThatAgentsFiles`

**C37** - `{agentId}` not of this tenant makes file routes return `404` and not leak another tenant (AGENT-05, AC 37)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentFileTests.Handle_ShouldThrow_WhenAgentIsInAnotherTenant`

**C38** - `DELETE /api/v1/ai/agents/{agentId}/files/{fileId}` removes the row; a later `GET` returns `404` (AGENT-05, AC 38)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteAgentFileTests.Handle_ShouldRemoveFile_WhenFileExists`

**C39** - Agent with `list_agent_files` and `read_agent_file` on the allowlist: when the LLM asks `read_agent_file`, the tool returns the persisted `content` of that `fileId` and only of that agent (AGENT-05, AC 39)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentFileToolTests.ReadAgentFile_ShouldReturnContent_ForSameAgent`

**C40** - This change does not create a vector store, embedding, hosted container, or MCP endpoint for these files (AGENT-05, AC 40)
Proof: `dotnet test tests/ArchitectureTests --filter FullyQualifiedName~SliceStructureTests.Features_ShouldNotContain_LayerFolders`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentFileToolTests.Module_ShouldNotExposeVectorOrMcpEndpoints`

**C44** - Unauthenticated file `GET` returns `401` (AGENT-05, Surface)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateAgentFileTests.Get_ShouldReturn401_WhenNotAuthenticated`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/v1/ai/agents` statuses (6) | 201 C1 · 400 C7 · 401 C41 · 403 C9 · 404 C8 · 409 C3 | - |
| `GET /api/v1/ai/agents` statuses (4) | 200 C2 · 401 C41 · 403 C9 · 404 C8 | - |
| `GET /api/v1/ai/agents/{agentId}` statuses (4) | 200 C5 · 401 C41 · 403 C9 · 404 C6 | - |
| `PUT /api/v1/ai/agents/{agentId}` statuses (6) | 200 C4 · 400 C7 · 401 C41 · 403 C9 · 404 C6 · 409 C3 | - |
| `DELETE /api/v1/ai/agents/{agentId}` statuses (5) | 204 C5 · 401 C41 · 403 C9 · 404 C6 · 409 C11 | - |
| `POST /api/v1/ai/chat` statuses (4) | 200 C13 · 401 C16 · 404 C14,C17 · 500 C34 | - |
| `POST /api/v1/ai/agents/{agentId}/files` statuses (5) | 201 C35 · 400 C35 · 401 C44 · 403 C9 · 404 C37 | - |
| `GET /api/v1/ai/agents/{agentId}/files` statuses (4) | 200 C36 · 401 C44 · 403 C9 · 404 C37 | - |
| `GET …/files/{fileId}` statuses (4) | 200 C39 · 401 C44 · 403 C9 · 404 C38 | - |
| `DELETE …/files/{fileId}` statuses (4) | 204 C38 · 401 C44 · 403 C9 · 404 C38 | - |
| LLM providers (2) | OpenRouter C30 · MicrosoftAgentFramework C30 | - |
| LLM registration (3) | Testing stub C31 · Development empty key stub C31 · Production fail-fast C32 | - |
| one-way doors (7) | Agent aggregate C1 · mutable Update C4 · unique name C3 · AgentFile C35 · LLM switch C30 · chat agentId C12,C13,C14 · RBAC C9,C16 | - |
| Relations entities (2) | Agent C1 · AgentFile C35 | - |
| screens (3) | agents-list C19,C20,C21,C22,C25,C42 · agent-form C23,C24 · chat C27,C28,C43 | - |
| agents-list copy (2) | empty `Nenhum agente` C19 · confirm `Desativar agente` C22 | - |
| chat copy (3) | `Faça uma pergunta` C28 · `A escrever…` C28 · picker label `Agente` C27 | - |

- Claims naming a status code, route or response shape: C1, C3, C6, C7, C8, C9, C11, C13, C14, C16, C17, C32, C34, C41, C44 - each has a proof that crosses the HTTP or host boundary
- No other check claims more than the single case its proof exercises

## Test policy

The repo answers where API tests live (`tests/Api.Tests/{Module}`) and that HTTP contracts go through `TestWebApplicationFactory`. It does not answer how much of a new provider switch must be asserted at the HTTP-client layer vs DI.

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide, crossed by a boundary | one at the boundary **and** one at its own level | contract at the boundary; one asserted case per decision-table row at its own level |
| Decide, not crossed by a boundary | one at its own level | one asserted case per decision-table row |
| Entry point that does not decide | one at the boundary | accepted input, each rejected input, each error path |
| Instrumentation, pass-through | none of its own | covered by the consumer's proof |

Evidence:

- `CreateAgentHandler` / `DeactivateAgentHandler`: unique name, last-active guard - decide, proven at handler level (C3, C11) and 409 at HTTP (C3)
- `LlmServiceResolver`: Testing/Development/Production × key present/absent × two providers - decide, proven at registration tests C31, C32 and HTTP-client tests C30
- `OpenRouterLlmService` / `MicrosoftAgentFrameworkLlmService`: map tools and messages then POST - decide, proven by fake handler asserting the BaseUrl (C30)
- `agents-list.ts` / `agent-form.ts` / `chat.ts`: screen state - decide, proven by Vitest+MSW (C19–C28)
- Endpoint `Map*` methods: instrumentation, covered by HTTP tests

Cost: DI/fail-fast tests plus one fake-handler test per provider. Without those rows a stub named Azure would still pass ChatAiHandlerTests.

## Swept

- validation: C7, C24 - unknown `toolNames` → 400; bounds of `name`/`instructions` in the CreateAgent validator; form shows `errors[campo]`
- failure and partial failure: C34 - LLM HTTP → 500; `IUnitOfWork.SaveChangesAsync` as Tenants - nothing persisted if the handler throws before save
- idempotency, retry, duplicates: C3 - unique name → 409. Chat and Update are not idempotent - same as Tenants
- authorization and rate limits: C9, C16, C41, C44; rate limit n/a - no limiter on these routes today
- concurrency and ordering: last-write-wins on Update (C4); two Creates with the same name: unique index (C3)
- data lifecycle: C5, C10, C11 (soft-delete + seed + last active); C38 (DELETE file removes the row). No TTL
- external-dependency failure: C34. No silent fallback to stub in Production when the key exists
- state transitions: active → inactive via Deactivate (C5); inactive is not the chat default (C14). No reactivate this round
- observability: C33 (usage provider/model). Existing ChatAiHandler logs stay. No new p95 metric

## Handoff

Arithmetic before code, `wc -c` of files each slice writes, divided by four:

- S1 CRUD+seed+policies ~45k chars → ~11k tokens
- S2 ChatAi agentId ~20k → ~5k
- S3 UI list/form/picker ~70k → ~18k
- S4 ILlmService switch + tests ~40k → ~10k
- S5 AgentFile + tools ~35k → ~9k
- reading already in context (Tenants analogue, Ai module, plan) ~40k

Total ~93k, under the 150k budget → **one builder**, no mid-feature handoff.
