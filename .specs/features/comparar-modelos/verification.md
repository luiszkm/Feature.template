# Comparar modelos — Verification

**Verdict**: FAIL
**Profile**: light (no `## tlc-implement` declaration in `AGENTS.md`)
**Diff range**: 7148c0c..20a47c6 (HEAD)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

61/62 checks proven. **C60 fails at HEAD**: `dotnet test tests/ArchitectureTests` is 15/17 in a
clean worktree of `20a47c6`. It goes green only against the dirty working tree. Commit `b563ded`
regenerated `src/Api/openapi.json` from a working tree whose uncommitted `GetRole.cs` had dropped
`GET /api/v1/authorization/roles/{roleId}`. So the committed contract lost that operation
(base `paths["/api/v1/authorization/roles/{roleId}"]` = `delete,get,put`, HEAD = `delete,put`).
HEAD's `GetRole.cs:26` still maps it and HEAD's `features.json` still lists `GetRole`.

Failing at HEAD:
- `OpenApiContractTests.EveryFeatureRoute_ShouldExist_InTheDocument` (`OpenApiContractTests.cs:75`) - "Routes in features.json missing from openapi.json: GET /api/v1/authorization/roles/{roleId}"
- `OpenApiContractTests.Document_ShouldCover_EveryFeatureRoute` (`OpenApiContractTests.cs:95`) - Expected 43, Actual 42

The 4 new routes themselves are in the committed `openapi.json` (`/ai/models` get, `/ai/comparisons` post+get, `/ai/comparisons/{comparisonId}` get) and in `features.json`.

## Steps run / not run

- Step 1 (binding-source enumeration, `ui`): **not run** because of the profile. Only a light comparison of checks against `.tasks/comparar-modelos-api.md` was done on concrete values (see "Source sanity").
- Step 2 (run every proof): run. Backend: one batched `dotnet test` at the working tree **and** again in a clean `git worktree` at HEAD. Front: one batched run in each tree.
- Step 3 (assertion check, evidence-or-zero): run. Also read the `Swept` rows marked existing.
- `Coverage` recomputation, `Test policy` verdicts, step 4 fault injection (`standard`/`ui`): **not run** because of the profile.
- The real tree's `git status --porcelain` was the same before and after. One `.angular/` cache dir that the worktree front run created at the repo root was removed.

## Proof runs

| Tree | Command | Result |
|---|---|---|
| working tree | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AgentModelTests\|~ListModelsTests\|~AgentLoopUsageTests\|~AiUsageTests\|~CompareModelsTests\|~ChatAiHandlerTests\|~LlmServiceTests\|~ChatAiTests" --logger trx` | 83 passed, 0 failed |
| HEAD worktree | same | 83 passed, 0 failed |
| working tree | `dotnet test tests/ArchitectureTests` | 17 passed |
| **HEAD worktree** | `dotnet test tests/ArchitectureTests` | **15 passed, 2 failed** |
| working tree + HEAD worktree | `node node_modules/@angular/cli/bin/bootstrap.js test --no-watch --include src/app/features/ai/agent-form.spec.ts --include src/app/architecture.spec.ts --reporters=verbose` | 17 passed (2 files) in both |

Substitution: `npx ng test` exits 3 on the local Node v24.11.1 (Angular CLI version gate). The CLI
bootstrap was called directly, which skips only that gate. `--filter` was replaced by running the
whole spec files with the verbose reporter. Each named `it(...)` shows as `✓` below.

All 59 distinct backend test names in the checklist matched a passed TRX entry (none missing). The
`LlmServiceTests.*` filters match classes `OpenRouterLlmServiceTests` / `MicrosoftAgentFrameworkLlmServiceTests`
by substring. `Post_ShouldReturn400_WhenInputInvalid` ran 10 cases: `1 modelo`, `5 modelos`,
`ids repetidos`, `id fora do catálogo`, `prompt vazio`, `prompt 4001`, `4 anexos`,
`anexos 100001 chars`, `name vazio`, `name 201`. All named tests except the C61 guard are added or
touched in `7148c0c..HEAD`. `architecture.spec.ts` is a pre-existing guard that this feature's new
routes rely on. That is acceptable for its claim.

Working-tree noise: an uncommitted `TestServiceFactory.cs` hunk (unique InMemory DB name), an
uncommitted method-aware rewrite of `architecture.spec.ts`, and an uncommitted `features.json`
(drops `GetRole`, which masks the C60 failure). The HEAD reruns rule these out for every check
except C60.

## Checks

| Check | Claim | Proof run | Evidence | Result |
|---|---|---|---|---|
| C1 | POST with catalog model → 201, `model` echoed | AgentModelTests.Post_ShouldReturn201_WithModel_WhenModelInCatalog passed | `AgentModelTests.cs:28` `Assert.Equal(HttpStatusCode.Created, …)`; `:30` `Assert.Equal(StubModelCatalog.ModelA, agent!.Model)` | PASS |
| C2 | POST without model → null stored/returned | …Post_ShouldStoreNullModel_WhenModelOmitted passed | `AgentModelTests.cs:48` `Assert.Null(created!.Model)`; `:50` `Assert.Null(stored!.Model)` | PASS |
| C3 | POST model not in catalog → 400 Validation failed, error on Model | …Post_ShouldReturn400_WhenModelNotInCatalog passed | `AgentModelTests.cs:67` BadRequest; `:69` `Assert.Equal("Validation failed", problem!.Title)`; `:70` `Assert.Contains("Model", problem.Errors.Keys)` | PASS |
| C4 | PUT model not in catalog → 400 | …Put_ShouldReturn400_WhenModelNotInCatalog passed | `AgentModelTests.cs:88` BadRequest; `:90` title `Validation failed`; `:91` `Model` key | PASS |
| C5 | model 201 chars → 400 | …Post_ShouldReturn400_WhenModelExceeds200Chars passed | `AgentModelTests.cs:108` BadRequest; `:110` title. Note: a 201-char id also fails the catalog rule (`CreateAgent.cs:25`), so this does not tell the length rule (`CreateAgent.cs:24` `MaximumLength(200)`) apart from the catalog rule | PASS (weak) |
| C6 | PUT without model clears it | …Put_ShouldClearModel_WhenModelOmitted passed | `AgentModelTests.cs:120` precondition `ModelB`; `:129` OK; `:131` `Assert.Null(stored!.Model)` | PASS |
| C7 | agent model on every LLM call incl. summary | ChatAiHandlerTests.Handle_ShouldSendAgentModel_OnEveryLlmCall_IncludingSummary passed | `ChatAiHandlerTests.cs:202` `Assert.Equal(6, llm.Requests.Count)`; `:203` `Assert.All(llm.Requests, r => Assert.Equal(StubModelCatalog.ModelA, r.Model))` | PASS |
| C8 | agent model null → LlmRequest.Model null | …Handle_ShouldSendNullModel_WhenAgentHasNoModel passed | `ChatAiHandlerTests.cs:222` `Assert.All(llm.Requests, r => Assert.Null(r.Model))`. Note: nothing asserts that `Requests` is non-empty, so the `Assert.All` would pass on an empty list | PASS (weak) |
| C9 | OpenRouter null model → sends `Ai:Llm:Model` | LlmServiceTests.OpenRouter_ShouldSendConfiguredModel_WhenRequestModelIsNull passed | `LlmServiceTests.cs:47` `Assert.Equal("openai/gpt-4o-mini", ModelOf(handler.RequestBodies.Single()))` | PASS |
| C10 | OpenRouter `Model="a/b"` → payload `"model":"a/b"` | …OpenRouter_ShouldSendRequestModel_WhenSet passed | `LlmServiceTests.cs:58` `Assert.Equal("a/b", ModelOf(...))` | PASS |
| C11 | seed agent model null | AgentModelTests.DefaultAgent_ShouldHaveNullModel passed | `AgentModelTests.cs:145-146` `Assert.NotNull(seed); Assert.Null(seed.Model)` | PASS |
| C12 | GET list + GET by id carry model | …GetAndList_ShouldReturnModel passed | `AgentModelTests.cs:159` `Assert.Equal(ModelA, single!.Model)`; `:160` same on `page!.Data` | PASS |
| C13 | agent-form sends chosen model / null with Padrão do sistema | `envia o modelo escolhido` ✓, `envia model null com Padrão do sistema` ✓ | `agent-form.spec.ts:88` `expect(created.model).toBe('b/model')`; `:98` `expect(updated.model).toBe('a/model')`; `:120` `expect(updated).toHaveProperty('model', null)` | PASS |
| C14 | form shows agent model or Padrão do sistema | `mostra o modelo do agente` ✓ | `agent-form.spec.ts:134-135` `select.value` / option text `'b/model'`; `:145-146` `''` / `'Padrão do sistema'` | PASS |
| C15 | catalog fails → message + save keeps model | `catálogo indisponível mantém o modelo` ✓ | `agent-form.spec.ts:166` `toBe('Catálogo de modelos indisponível')`; `:169` `toHaveProperty('model', 'a/model')` | PASS |
| C16 | GET /models: 200, tools-only, 5 fields, id asc | ListModelsTests.OpenRouter_ShouldReturnOnlyToolModels_SortedById passed | `ListModelsTests.cs:40` OK; `:43` ids `{"a/tools","z/tools"}` (drops `m/no-tools`, sorted); `:45-47` field set `contextLength,id,inputPricePerToken,name,outputPricePerToken` | PASS |
| C17 | AllowedModels intersects | …OpenRouter_ShouldIntersectWithAllowedModels passed | `ListModelsTests.cs:63` `Assert.Equal(new[] { "z/tools" }, …)` (allowed = z/tools, m/no-tools, missing/model) | PASS |
| C18 | MAF → Model ∪ AllowedModels, null prices | …Maf_ShouldReturnConfiguredModels_WithNullPrices passed | `ListModelsTests.cs:78` ids `{"gpt-4o","gpt-4o-mini"}`; `:81-82` prices null. Note: the fixture's `Model` (`gpt-4o-mini`) is already in `AllowedModels`, so the union is never observed. An implementation that returned only `AllowedModels` would also pass. The code does union (`ModelCatalog.cs:108-109`) | PASS (weak) |
| C19 | stub → Model, stub/model-a, stub/model-b, prices 0 | …Get_ShouldReturnStubCatalog_InTesting passed | `ListModelsTests.cs:94-96` ids `cfg/model, stub/model-a, stub/model-b` (ordinal); `:99-100` `Assert.Equal(0m, …)` | PASS |
| C20 | /models fails → 503 Service unavailable | …Get_ShouldReturn503_WhenProviderCatalogFails passed | `ListModelsTests.cs:113` ServiceUnavailable; `:115` `Assert.Equal("Service unavailable", problem!.Title)` (502 upstream only. Task 18's `HttpRequestException` and timeout variants are not exercised) | PASS |
| C21 | catalog down → POST /agents with model 503 | AgentModelTests.Post_ShouldReturn503_WhenCatalogUnavailable passed | `AgentModelTests.cs:178` ServiceUnavailable; `:180` title | PASS |
| C22 | success cached (1h) | ListModelsTests.OpenRouter_ShouldCacheSuccess passed | `ListModelsTests.cs:127` `Assert.Equal(1, handler.Calls)`. The 1h TTL is not asserted; it is in code at `ModelCatalog.cs:42` `TimeSpan.FromHours(1)` | PASS |
| C23 | failure not cached | …OpenRouter_ShouldNotCacheFailure passed | `ListModelsTests.cs:138` throws `ServiceUnavailableException`; `:141` `Assert.Equal(2, handler.Calls)`; `:142` 2 models | PASS |
| C24 | no ai.agent.read → 403 | …Get_ShouldReturn403_WithoutAgentRead passed | `ListModelsTests.cs:153` `Assert.Equal(HttpStatusCode.Forbidden, …)` | PASS |
| C25 | OpenRouter maps usage + cost | LlmServiceTests.OpenRouter_ShouldMapUsageAndCost passed | `LlmServiceTests.cs:72-74` `120`, `30`, `0.00042m` | PASS |
| C26 | missing usage.cost → null | …OpenRouter_ShouldReturnNullCost_WhenCostMissing passed | `LlmServiceTests.cs:88` `Assert.Null(response.Cost)` | PASS |
| C27 | MAF maps tokens, cost null | …Maf_ShouldMapInputOutputTokens_WithNullCost passed | `LlmServiceTests.cs:165-167` `40`, `7`, `Assert.Null(response.Cost)` | PASS |
| C28 | AgentResult sums tokens and cost | AgentLoopUsageTests.RunAsync_ShouldSumTokensAndCost passed | `AgentLoopUsageTests.cs:23-25` `30`, `12`, `0.0035m` (10+20, 5+7, 0.001+0.0025) | PASS |
| C29 | any null cost → null | …RunAsync_ShouldReturnNullCost_WhenAnyCallCostIsNull passed | `AgentLoopUsageTests.cs:44` `Assert.Null(result.Cost)` | PASS |
| C30 | chat 200 → exactly one entry with fields | AiUsageTests.Chat_ShouldPersistOneUsageEntry_OnSuccess passed | `AiUsageTests.cs:33` `Assert.Single(...)`; `:34` agentId; `:35` `AiUsageOperations.Chat` (= `"chat"`, `AiUsageEntry.cs:46`); `:36` `"stub"` (effective model); `:37-39` `3`,`2`,`0.004m`; `:40` `True(Success)`; `:41` `Null(ErrorCode)` | PASS |
| C31 | model is agent's when set | …Chat_ShouldRecordAgentModel_WhenAgentHasModel passed | `AiUsageTests.cs:58` `Assert.Equal(StubModelCatalog.ModelB, Assert.Single(...).Model)` | PASS |
| C32 | loop throws → failed row | …Chat_ShouldPersistFailedEntry_WhenLoopThrows passed | `AiUsageTests.cs:77` `False(Success)`; `:78` `nameof(HttpRequestException)`; `:79-80` `0`,`0`; `:81` `Null(Cost)` | PASS |
| C33 | same exception rethrown | …Chat_ShouldRethrowOriginalException_WhenLoopThrows passed | `AiUsageTests.cs:99` `Assert.Same(original, thrown)` | PASS |
| C34 | save fails → LogError, chat returns | …Tracker_ShouldLogAndSwallow_WhenSaveFails passed | `AiUsageTests.cs:124` `Assert.Equal("fine", result.Reply)`; `:131` `Contains(… Level == LogLevel.Error && Message.Contains("Failed to persist AI usage"))` | PASS |
| C35 | stub → provider "stub" | …Chat_ShouldRecordStubProvider_InTesting passed | `AiUsageTests.cs:145` `Assert.Equal("stub", Assert.Single(...).Provider)` | PASS |
| C36 | entry holds only metadata | …UsageEntry_ShouldHoldOnlyMetadataProperties passed | `AiUsageTests.cs:153` exact property set `AgentId, Cost, CreatedAt, ErrorCode, Id, InputTokens, LatencyMs, Model, Module, Operation, OutputTokens, Provider, Success, TenantId` | PASS |
| C37 | repo only add/read | …UsageRepository_ShouldExposeNoUpdateOrDelete passed | `AiUsageTests.cs:167` `Assert.Equal(new[] { "AddAsync", "ListAsync" }, methods)` | PASS |
| C38 | reads tenant-filtered | …UsageEntries_ShouldBeTenantFiltered passed | `AiUsageTests.cs:182` `Single(… TenantId)`; `:183` `Empty(… Guid.NewGuid())` | PASS |
| C39 | chat response stays `{reply, iterationsUsed}` | ChatAiTests.ChatAi_ShouldReturnOnlyReplyAndIterations passed | `ChatAiTests.cs:105-107` property names == `{"iterationsUsed","reply"}` | PASS |
| C40 | POST → 201, Location, results in request order | CompareModelsTests.Post_ShouldReturn201_WithOneResultPerModel_InRequestOrder passed | `CompareModelsTests.cs:43` Created; `:45` Location `/api/v1/ai/comparisons/{id}`; `:46` `Assert.Equal(TwoModels, Results.Select(r => r.Model))` with ModelA delayed 150 ms (`:28-29`) | PASS |
| C41 | per-model Model, Instructions, tools, Temperature 0.2 | …Handle_ShouldRunAgentPerModel_WithSameInstructionsToolsAndTemperature passed | `CompareModelsTests.cs:306` models set; `:309` `"Be terse."` SystemPrompt; `:310` tools `[GetTenantInfo]`; `:311` `Assert.Equal(0.2f, r.Temperature)` | PASS |
| C42 | attachments appended identically | …Handle_ShouldAppendAttachmentsToPrompt_IdenticallyForAllModels passed | `CompareModelsTests.cs:327-328` `"Summarise\n\n--- a.txt ---\nalpha\n\n--- b.csv ---\nx,y"` for all; `:329` count 2 | PASS |
| C43 | Succeeded result fields | …Handle_ShouldFillSucceededResult passed | `CompareModelsTests.cs:343-350` `"Succeeded"`, `"fine"`, `11`, `5`, `0.0007m`, `LatencyMs >= 0`, `1`, `Null(ErrorCode)` | PASS |
| C44 | one model throws → Failed, others Succeeded | …Handle_ShouldIsolateFailure_ToOneModel passed | `CompareModelsTests.cs:365-368` `"Failed"`, `Null(Reply)`, `nameof(HttpRequestException)`, other `"Succeeded"` | PASS |
| C45 | timeout → TimedOut / "Timeout" | …Handle_ShouldMarkTimedOut_WhenModelExceedsTimeout passed | `CompareModelsTests.cs:387-389` `"Succeeded"`, `"TimedOut"`, `"Timeout"` | PASS |
| C46 | all fail → 201 and persisted | …Post_ShouldReturn201_AndPersist_WhenAllModelsFail passed | `CompareModelsTests.cs:64` Created; `:67` stored 2 results via GET; `:68` all `"Failed"` | PASS |
| C47 | caller token cancelled → completes and persists | …Handle_ShouldCompleteAndPersist_WhenCallerTokenIsCancelled passed | `CompareModelsTests.cs:406` `True(caller.IsCancellationRequested)`; `:407` all `"Succeeded"`; `:410` `NotNull(GetByIdAsync(...))` | PASS |
| C48 | totalCost sum of non-null / null if all null | …Handle_ShouldSumNonNullCosts, …Handle_ShouldReturnNullTotalCost_WhenAllCostsNull passed | `CompareModelsTests.cs:424` `Assert.Equal(0.003m, output.TotalCost)` (0.003 + null); `:436` `Assert.Null(output.TotalCost)` | PASS |
| C49 | usage entry per model with "compare" | …Handle_ShouldRecordUsageEntryPerModel passed | `CompareModelsTests.cs:449-450` filter `AiUsageOperations.Compare` (= `"compare"`, `AiUsageEntry.cs:47`); models == `TwoModels` | PASS |
| C50 | validation 400, 10 cases | …Post_ShouldReturn400_WhenInputInvalid passed ×10 | `CompareModelsTests.cs:97` `Assert.True(StatusCode == BadRequest, …)`; `:99` `Assert.Equal("Validation failed", problem!.Title)`. The 10 case names listed above match the claim one to one | PASS |
| C51 | agent missing → 404 | …Post_ShouldReturn404_WhenAgentMissing passed | `CompareModelsTests.cs:115` NotFound | PASS |
| C52 | agent inactive → 404 | …Post_ShouldReturn404_WhenAgentInactive passed | `CompareModelsTests.cs:124` precondition DELETE 204; `:133` NotFound | PASS |
| C53 | MAF → 409 Business rule violation + detail | …Handle_ShouldThrowBusinessRule_WhenProviderIsMaf, …Post_ShouldReturn409_WhenProviderIsMaf passed | `CompareModelsTests.cs:151` Conflict; `:153` `"Business rule violation"`; `:154` `"Comparação requer o provider OpenRouter"`; `:460` same message on the exception | PASS |
| C54 | list: tenant page, createdAt desc, defaults 1/20, item fields | …List_ShouldReturnTenantPage_NewestFirst passed | `CompareModelsTests.cs:191-192` `1`, `20`; `:194` `[second, first]`; `:196-199` agentName, `new string('q', 200)` preview, models, `0.004m`. Task 44 also says "estável por `id`"; neither the check nor the test covers the tiebreak (precision gap) | PASS |
| C55 | GET /{id} → full ComparisonOutput | …Get_ShouldReturnComparison passed | `CompareModelsTests.cs:213` OK; `:215-227` agentId, agentName, `"hello"`, attachments `["notes.md"]`, `0.02m`, results model/status/reply/tokens/cost | PASS |
| C56 | other tenant or missing → 404 | …Get_ShouldReturn404_ForOtherTenantOrMissing passed | `CompareModelsTests.cs:236` HTTP NotFound for a missing id; `:242` `ThrowsAsync<NotFoundException>` for the other tenant. The other-tenant half is proven only at handler level (partial level gap) | PASS |
| C57 | POST without manage → 403 | …Post_ShouldReturn403_WithoutAgentManage passed | `CompareModelsTests.cs:260` Forbidden | PASS |
| C58 | list/detail without read → 403 | …Get_ShouldReturn403_WithoutAgentRead passed | `CompareModelsTests.cs:269-270` Forbidden on both routes | PASS |
| C59 | EnableAI=false → 4 routes 404 Feature disabled | …NewRoutes_ShouldReturn404_WhenAiDisabled passed | `CompareModelsTests.cs:278-283` the 4 routes; `:289` NotFound; `:291` `"Feature disabled"` | PASS |
| C60 | features.json ↔ openapi.json agree | `dotnet test tests/ArchitectureTests`: working tree 17/17, **HEAD 15/17** | HEAD: `OpenApiContractTests.cs:75` "missing from openapi.json: GET /api/v1/authorization/roles/{roleId}"; `:95` Expected 43 / Actual 42. The 4 new routes are present in both files | **FAIL** |
| C61 | every features.json route has a front client | `todas as rotas de features.json tem cliente` ✓ (working tree and HEAD) | `architecture.spec.ts` `expect(missing).toEqual([])`; clients at `compare.ts:21,25,29-30,34` for the 4 routes | PASS |
| C62 | catalog down → POST /comparisons 503 | CompareModelsTests.Post_ShouldReturn503_WhenCatalogUnavailable passed | `CompareModelsTests.cs:172` ServiceUnavailable; `:174` `"Service unavailable"` | PASS |

## Swept rows marked existing

| Row | Cited constraint | Found |
|---|---|---|
| authorization: rest existing (`AiAgentsRead`/`AiAgentsManage`) | policies on the new endpoints | yes. `ListModels.cs:29` `AiAgentsRead`, `CompareModels.cs:194` `AiAgentsManage`, `ListModelComparisons.cs:55` `AiAgentsRead`, `GetModelComparison.cs:37` `AiAgentsRead`. Each also has `RequireFeature(EnableAI)` (`:26`, `:191`, `:52`, `:34`) |
| (Landing) `503` pattern | `ServiceUnavailableException` → 503 | yes. `Kernel.cs:27`, `ExceptionHandlerExtensions.cs:58-64` |

## Source sanity (checks vs `.tasks/comparar-modelos-api.md`)

No check contradicts the task on a concrete value: the status codes, titles (`Validation failed`,
`Service unavailable`, `Business rule violation`, `Feature disabled`), detail copy, field names,
the `chat`/`compare` literals and the 0.2 temperature all match. Omissions against the source (not
contradictions):
- Task 18: "`POST`/`PUT` de um agente com `model` não nulo … `503`". No check covers the PUT branch (C21 is POST only).
- Task 44: "estável por `id`". The tiebreak is not in C54 and is not asserted.
- Task 18 names three failure kinds (`HttpRequestException`, non-2xx, timeout). Only non-2xx is exercised (C20 502, C23 500).

## Gate

`dotnet test tests/ArchitectureTests` at HEAD `20a47c6` (clean worktree) - 15 passed, 2 failed
`dotnet test tests/Api.Tests --filter <8 classes>` at HEAD - 83 passed, 0 failed
`ng test` (CLI bootstrap) agent-form.spec + architecture.spec at HEAD - 17 passed, 0 failed
