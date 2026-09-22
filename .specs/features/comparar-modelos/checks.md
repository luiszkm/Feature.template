# Comparar modelos — API, modelo por agente e custo

tlc-implement · profile **light** (nenhuma declaração `## tlc-implement` no `AGENTS.md`) · handoff on, budget 150k

Base: `7148c0c` no branch `feat/comparar-modelos`, com 19 alterações pendentes de trabalho anterior
fora do âmbito — só entra em commit o que esta feature muda (stage por hunk em `features.json`,
`openapi.json`, `TestServiceFactory.cs`). Linha de base: `Api.Tests` 131/131, `ArchitectureTests` 17/17.

Sources:

- `.tasks/comparar-modelos-api.md` — critérios 1–46, Decided, Surface (a fonte)
- `.design/comparar-modelos.md` — Journey, Boundary, Decisions
- `.specs/features/observabilidade-agente/plan.md` S1 — AC 1–9, doors 1/3/6 absorvidas (AD-009)
- conversa 2026-09-22 — branch novo + commits por slice, só com o que esta feature muda

## Out of scope

- Ecrãs `/ai/compare`, `/ai/comparisons` — `.tasks/comparar-modelos-telas.md`
- Anexos multimodais/PDF, teto de custo, parâmetros editáveis, streaming, `202` assíncrono — design Boundary
- W1 S2/S3, W2, W8 — outras features
- Filtros na lista, apagar comparação, retenção, rate limit — Unresolved 1, 2 da task

## Landing

Toca `Features/Ai` (contratos LLM, providers, loop, agente, slices novos), `Shared/Kernel.cs` e
`Host/Extensions/ExceptionHandlerExtensions.cs` (503), e no front `ai.contracts.ts`, `agent-form.ts`,
`compare.ts` (clientes). Reusa `AgentLoop`, `ToolRegistry`, `AiTenantQueryFilters`, `ListQuery`/
`ToPaginatedListAsync`, `EfRepositoryHelpers`, `TenantContext.SetTenant`, `IMemoryCache` (já registado
por Identity), `RequireFeature`.

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| `Agent.Model` | `string?`, `HasMaxLength(200)`, nullable; `AgentOutput(..., string? Model)` no fim; `CreateAgentCommand`/`UpdateAgentRequest` com `string? Model = null` | coluna obrigatória — obriga a backfill com o modelo global |
| Contrato LLM | `LlmRequest(..., string? Model = null)`; `LlmResponse(string Text, int TotalTokens, IReadOnlyList<ToolCall>? ToolCalls = null, int InputTokens = 0, int OutputTokens = 0, decimal? Cost = null)` | custo `0` quando ausente — confunde grátis com desconhecido |
| Ledger `AiUsageEntry` | tabela `AiUsageEntries`: `TenantId`, `AgentId` sem FK, `Provider`≤100, `Model`≤200, `Module`≤50, `Operation`≤50, `InputTokens`, `OutputTokens`, `Cost decimal(18,8) null`, `LatencyMs`, `Success`, `ErrorCode`≤200, `CreatedAt`; repo só `AddAsync` + leitura | FK para `AiAgents` — perde histórico ao apagar |
| Agregado `ModelComparison` | `AiModelComparisons` (`TenantId`, `AgentId` FK restrict, `Prompt`≤4000, `Attachments` JSON, `CreatedByUserId Guid?`, `CreatedAt`) + `OwnsMany` `ModelComparisonResult` em `AiModelComparisonResults` (`Position`, `Model`≤200, `Status` string ∈ {`Succeeded`,`Failed`,`TimedOut`}, `Reply`, `InputTokens`, `OutputTokens`, `Cost decimal(18,8) null`, `LatencyMs`, `IterationsUsed`, `ErrorCode`≤200) | resultados como agregado próprio — não há actualização por célula |
| Contratos HTTP | `GET /api/v1/ai/models`; `POST/GET /api/v1/ai/comparisons`; `GET /api/v1/ai/comparisons/{comparisonId}` — shapes de `.tasks/comparar-modelos-api.md` Decided | `202` assíncrono — só com lotes |
| `503` padrão novo | `ServiceUnavailableException` em `Shared/Kernel.cs` → `503`, title `Service unavailable` em `ExceptionHandlerExtensions` | `Results.Problem` local por endpoint — 4 cópias |
| Execução paralela | scope DI por modelo, `TenantContext.SetTenant` no filho, `CancelAfter(Ai:Llm:CompareTimeoutSeconds)`, sem `RequestAborted` | scope do pedido — `AppDbContext` não aceita concorrência |
| Migration | uma migration `AddModelComparison` em `src/Api/Shared/Migrations` com as 4 mudanças acima | migrations separadas por bloco — mesma feature, mesmo deploy |

- Nada mais nesta mudança é difícil de reverter.

## Checks

### S1 — Modelo por agente · ~14 files · ~95 KB · ~24k

**C1** — `POST /api/v1/ai/agents` com `model` do catálogo → `201` e `AgentOutput.model` igual (task 1)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Post_ShouldReturn201_WithModel_WhenModelInCatalog`

**C2** — `POST` sem `model` grava `null` e devolve `model: null` (task 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Post_ShouldStoreNullModel_WhenModelOmitted`

**C3** — `POST` com `model` fora do catálogo → `400` `Validation failed` com erro em `Model` (task 3)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Post_ShouldReturn400_WhenModelNotInCatalog`

**C4** — `PUT` com `model` fora do catálogo → `400` `Validation failed` (task 3)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Put_ShouldReturn400_WhenModelNotInCatalog`

**C5** — `model` com 201 caracteres → `400` (task 4)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Post_ShouldReturn400_WhenModelExceeds200Chars`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Validator_ShouldRejectModelOver200Chars_EvenWhenInCatalog` (round 2 — isola a regra de tamanho da do catálogo)

**C6** — `PUT` sem `model` num agente com `model` limpa para `null` (task 5)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Put_ShouldClearModel_WhenModelOmitted`

**C7** — chat com agente com `model` envia `LlmRequest.Model` igual em todas as chamadas, incluindo o resumo após 5 iterações (task 6)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldSendAgentModel_OnEveryLlmCall_IncludingSummary`

**C8** — chat com agente `model = null` envia `LlmRequest.Model = null` (task 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldSendNullModel_WhenAgentHasNoModel`

**C9** — OpenRouter com `LlmRequest.Model = null` envia `Ai:Llm:Model` no payload (task 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceTests.OpenRouter_ShouldSendConfiguredModel_WhenRequestModelIsNull`

**C10** — OpenRouter com `LlmRequest.Model = "a/b"` envia `"model":"a/b"` (task 8)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceTests.OpenRouter_ShouldSendRequestModel_WhenSet`

**C11** — seed do `DefaultAgentProvisioner` tem `model = null` (task 9)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.DefaultAgent_ShouldHaveNullModel`

**C12** — `GET /api/v1/ai/agents` e `GET /api/v1/ai/agents/{id}` trazem `model` (task 10)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.GetAndList_ShouldReturnModel`

**C13** — `agent-form` envia `model` escolhido no `POST`/`PUT`, e `null` com `Padrão do sistema` (task 11)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agent-form.spec.ts --filter "envia o modelo escolhido"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agent-form.spec.ts --filter "envia model null com Padrão do sistema"`

**C14** — `agent-form` de agente existente mostra o `model` dele, ou `Padrão do sistema` quando `null` (task 12)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agent-form.spec.ts --filter "mostra o modelo do agente"`

**C15** — catálogo falha no `agent-form` → `Catálogo de modelos indisponível` e o save envia o `model` que o agente tinha (task 13)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/agent-form.spec.ts --filter "catálogo indisponível mantém o modelo"`

### S2 — Catálogo de modelos · ~6 files · ~30 KB · ~8k

**C16** — `GET /api/v1/ai/models` (OpenRouter) → `200`, só modelos com `"tools"` em `supported_parameters`, campos `id,name,contextLength,inputPricePerToken,outputPricePerToken`, ordem `id` asc (task 14)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.OpenRouter_ShouldReturnOnlyToolModels_SortedById`

**C17** — `AllowedModels` não vazio filtra à intersecção (task 15)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.OpenRouter_ShouldIntersectWithAllowedModels`

**C18** — provider MAF devolve `Ai:Llm:Model` ∪ `AllowedModels` com preços `null` (task 16)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.Maf_ShouldReturnConfiguredModels_WithNullPrices`

**C19** — stub devolve `Ai:Llm:Model`, `stub/model-a`, `stub/model-b` com preços `0` (task 17)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.Get_ShouldReturnStubCatalog_InTesting`

**C20** — `{BaseUrl}/models` falha → `GET /api/v1/ai/models` `503` `Service unavailable` (task 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.Get_ShouldReturn503_WhenProviderCatalogFails`

**C21** — catálogo inacessível → `POST` e `PUT /agents` com `model` não nulo `503` (task 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Post_ShouldReturn503_WhenCatalogUnavailable`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentModelTests.Put_ShouldReturn503_WhenCatalogUnavailable` (round 2 — o task 18 nomeia também o PUT)

**C22** — sucesso fica em cache 1h: segunda chamada não faz novo pedido a `/models` (task 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.OpenRouter_ShouldCacheSuccess`

**C23** — falha não fica em cache: pedido seguinte volta a chamar `/models` (task 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.OpenRouter_ShouldNotCacheFailure`

**C24** — sem `ai.agent.read` → `403` (task 20)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListModelsTests.Get_ShouldReturn403_WithoutAgentRead`

### S3 — Uso e custo por chamada · ~10 files · ~45 KB · ~12k

**C25** — OpenRouter mapeia `prompt_tokens`/`completion_tokens`/`cost` para `InputTokens`/`OutputTokens`/`Cost` (task 21)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceTests.OpenRouter_ShouldMapUsageAndCost`

**C26** — OpenRouter sem `usage.cost` → `Cost = null` (task 21)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceTests.OpenRouter_ShouldReturnNullCost_WhenCostMissing`

**C27** — MAF mapeia `InputTokenCount`/`OutputTokenCount` e `Cost = null` (task 22)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~LlmServiceTests.Maf_ShouldMapInputOutputTokens_WithNullCost`

**C28** — `AgentResult` soma tokens de entrada/saída e custo sobre as chamadas do loop (task 23)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentLoopUsageTests.RunAsync_ShouldSumTokensAndCost`

**C29** — `AgentResult.Cost` é `null` se alguma chamada devolveu `Cost = null` (task 23)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentLoopUsageTests.RunAsync_ShouldReturnNullCost_WhenAnyCallCostIsNull`

**C30** — chat `200` grava exactamente uma `AiUsageEntry` com `agentId`, `operation = "chat"`, modelo efectivo, tokens, `cost`, `success = true`, `errorCode = null` (task 24)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.Chat_ShouldPersistOneUsageEntry_OnSuccess`

**C31** — `model` gravado é o do agente quando existe (task 24)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.Chat_ShouldRecordAgentModel_WhenAgentHasModel`

**C32** — loop lança → linha `success = false`, `errorCode` = nome do tipo, tokens `0`, `cost = null` (task 25)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.Chat_ShouldPersistFailedEntry_WhenLoopThrows`

**C33** — loop lança → a mesma excepção sobe ao caller (task 25)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.Chat_ShouldRethrowOriginalException_WhenLoopThrows`

**C34** — gravação da linha falha → `LogError` e o chat devolve o resultado sem lançar (task 26)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.Tracker_ShouldLogAndSwallow_WhenSaveFails`

**C35** — stub grava `provider = "stub"` (task 27)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.Chat_ShouldRecordStubProvider_InTesting`

**C36** — `AiUsageEntry` não tem propriedade de prompt, histórico nem resposta (task 28)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.UsageEntry_ShouldHoldOnlyMetadataProperties`

**C37** — `IAiUsageRepository` só tem métodos de adicionar/ler (task 29)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.UsageRepository_ShouldExposeNoUpdateOrDelete`

**C38** — leitura de `AiUsageEntry` filtra pelo tenant corrente (task 29)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTests.UsageEntries_ShouldBeTenantFiltered`

**C39** — resposta de `POST /api/v1/ai/chat` continua `{ reply, iterationsUsed }` (task 30)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturnOnlyReplyAndIterations`

### S4 — Comparação · ~10 files · ~60 KB · ~15k

**C40** — `POST /api/v1/ai/comparisons` válido → `201`, `Location` `/api/v1/ai/comparisons/{id}`, um resultado por modelo na ordem de `models` (task 31)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn201_WithOneResultPerModel_InRequestOrder`

**C41** — cada modelo recebe `LlmRequest.Model` = esse modelo, as `Instructions` do agente como system prompt, as tools do agente e `Temperature` 0.2 (task 32)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldRunAgentPerModel_WithSameInstructionsToolsAndTemperature`

**C42** — anexos entram no user prompt como `\n\n--- {name} ---\n{content}` por ordem, igual em todos os modelos (task 33)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldAppendAttachmentsToPrompt_IdenticallyForAllModels`

**C43** — resultado bem-sucedido tem `Succeeded`, `reply`, tokens, `cost`, `latencyMs`, `iterationsUsed`, `errorCode = null` (task 34)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldFillSucceededResult`

**C44** — um modelo lança → esse `Failed`, `reply = null`, `errorCode` = nome do tipo; os outros `Succeeded` (task 35)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldIsolateFailure_ToOneModel`

**C45** — um modelo excede `CompareTimeoutSeconds` → `TimedOut`, `errorCode = "Timeout"`; os outros terminam (task 36)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldMarkTimedOut_WhenModelExceedsTimeout`

**C46** — todos falham → `201` e comparação gravada (task 37)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn201_AndPersist_WhenAllModelsFail`

**C47** — token do pedido cancelado não cancela os modelos; comparação gravada (task 38)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldCompleteAndPersist_WhenCallerTokenIsCancelled`

**C48** — `totalCost` = soma dos `cost` não nulos; `null` se todos nulos (task 39)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldSumNonNullCosts`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldReturnNullTotalCost_WhenAllCostsNull`

**C49** — cada modelo grava uma `AiUsageEntry` com `operation = "compare"` e o seu `model` (task 40)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldRecordUsageEntryPerModel`

**C50** — validação → `400` `Validation failed`, table-driven sobre: 1 modelo, 5 modelos, ids repetidos, id fora do catálogo, prompt vazio, prompt 4001, 4 anexos, anexos 100 001 chars no total, `name` vazio, `name` 201 (task 41)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn400_WhenInputInvalid`

**C51** — agente inexistente → `404` (task 42)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn404_WhenAgentMissing`

**C52** — agente inactivo → `404` (task 42)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn404_WhenAgentInactive`

**C53** — provider MAF → `409` `Business rule violation`, detail `Comparação requer o provider OpenRouter` (task 43)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Handle_ShouldThrowBusinessRule_WhenProviderIsMaf`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn409_WhenProviderIsMaf`

**C54** — `GET /api/v1/ai/comparisons` → página do tenant, `createdAt` desc, default `pageNumber` 1 / `pageSize` 20, itens com `comparisonId, agentId, agentName, promptPreview` (≤200), `models, totalCost, createdAt` (task 44)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.List_ShouldReturnTenantPage_NewestFirst`

**C55** — `GET /api/v1/ai/comparisons/{id}` → `200` com `ComparisonOutput` completo (task 45)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Get_ShouldReturnComparison`

**C56** — `GET /{id}` de outro tenant ou inexistente → `404` (task 45)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Get_ShouldReturn404_ForOtherTenantOrMissing`

**C57** — `POST` sem `ai.agent.manage` → `403` (task 46)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn403_WithoutAgentManage`

**C58** — `GET` lista e detalhe sem `ai.agent.read` → `403` (task 46)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Get_ShouldReturn403_WithoutAgentRead`

**C59** — `EnableAI = false` → as 4 rotas novas `404` `Feature disabled` (task 46)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.NewRoutes_ShouldReturn404_WhenAiDisabled`

**C62** — catálogo inacessível → `POST /api/v1/ai/comparisons` `503` (task 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CompareModelsTests.Post_ShouldReturn503_WhenCatalogUnavailable`

### Contrato (transversal)

**C60** — `features.json` e `openapi.json` concordam com as 4 rotas novas
Proof: `dotnet test tests/ArchitectureTests`

**C61** — toda rota de `features.json` tem cliente no front (inclui as 4 novas em `compare.ts`)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "todas as rotas de features.json tem cliente"`

## Swept

- validation: C3, C4, C5, C50
- failure modes: C32, C33, C34, C44, C46
- idempotency and retry: not in scope — cada `POST` é uma execução paga nova, sem dedup (task Swept)
- authorization: C24, C57, C58; resto existing — `AiAgentsRead`/`AiAgentsManage`
- concurrency and ordering: C40 (ordem de `models`), C44, C45 (isolamento por scope)
- data lifecycle: C6, C37; retenção — task Unresolved 1
- dependency failure: C20, C21, C23, C44, C45
- state transitions: not in scope — `Status` escrito uma vez
- observability: C30, C34, C35, C49

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `ModelComparisonResult.Status` (3) | `Succeeded` C43 · `Failed` C44 · `TimedOut` C45 | - |
| provider → catálogo (3) | OpenRouter C16 · MAF C18 · stub C19 | - |
| provider → uso/custo (3) | OpenRouter C25/C26 · MAF C27 · stub C35 | - |
| validação de comparação (10) | table-driven, 10 casos em C50 | - |
| `Operation` (2) | `chat` C30 · `compare` C49 | - |
| catálogo indisponível (3 consumidores) | `GET /models` C20 · `POST /agents` C21 · `POST /comparisons` C62 | - |
| config `Ai:Llm:CompareTimeoutSeconds` (2 assemblies) | `TestWebApplicationFactory` e `Program` partilham `AddAiModule` + `appsettings.json` — um só lugar | - |

- Claims com status, rota ou shape: C1–C6, C12, C16–C24, C39, C40, C46, C50–C59 — cada um tem proof HTTP via `TestWebApplicationFactory`, excepto C41–C45, C47–C49, que são sobre o handler e provam lá.

## Handoff

- Round 1 do Verifier (FAIL em C60): o `openapi.json` de `b563ded` foi gerado do working tree sujo, que tem uma alteração pendente e fora do âmbito em `GetRole.cs`, e perdeu `GET /authorization/roles/{roleId}`. Correcção: regenerado numa worktree limpa em HEAD e posto no índice sem tocar no ficheiro do working tree. Lição: gerar o contrato sempre a partir de uma árvore limpa quando há alterações alheias pendentes.


S1–S4 lêem os mesmos ~65 KB de `Features/Ai` + ~64 KB de testes + ~39 KB do front ai + 64 KB de `openapi.json` (regenerado, não lido) ≈ 45k tokens de leitura; soma dos slices ~59k < 150k → **um batch**, sem handoff. O orquestrador constrói e depois despacha o Verifier sobre `7148c0c..HEAD`.
