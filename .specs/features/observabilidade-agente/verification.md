# Observabilidade do loop do agente (W1) verification

**Verdict**: FAIL
**Profile**: ui
**Diff range**: dc21725..96a9989
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Verified at `96a9989`. Checks C10–C31, C33, C34. S1 (C1–C9) and C32 are superseded and not verified here. The supersession claim mostly holds. `tests/Api.Tests/Ai/AiUsageTests.cs` has the tests for C1 (`:19`), C2 (`:64`), C3 (`:88`), C4 (`:107`), C5 Testing trigger (`:141`), C6 (`:155`), C7 (`:169`) and C8 (`:177`). For C32, `AiModule.cs:39` registers `AddScoped<IAiUsageTracker, AiUsageTracker>` and `NoOpAiUsageTracker` no longer exists in `src` or `tests`. Two superseded items have no test in that file: C9 (two identical chats give two rows) and C5's second trigger (Development without an ApiKey). Those are for `comparar-modelos` to account for.

Step 5 (walk the flow with the user) cannot run from a sub-agent. The usage screen has not been walked with a human.

## Binding sources

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| plan S3 arrangement paragraph + checks.md `Binding screen` line | yes - `plan.md` S3, `checks.md` S3 header | none - `usage.ts:35-91` has `header>h1`, `app-list-state` wrapping `mat-table`, `mat-paginator` after it, no action, no search, no sort | No numbered check covers any of these: the h1 copy `Uso do AI`, the 7 columns and their order (Agente, Chamadas, Falhas, Tokens entrada, Tokens saída, Total, Último uso), no search box, no sort headers, no primary action, and the paginator sitting outside `app-list-state` (regions: header, list-state with table, paginator). `usage.spec.ts:55-67` asserts h1, columns, no search and no sort, but no check names it. Nothing asserts that the primary action is absent or how the regions are arranged |
| `src/web/src/app/features/ai/agents-list.ts` | yes - read in full | none - `usage.ts` is agents-list without `create-agent`, `search`, `matSort`/`mat-sort-header`, the empty-state `emptyAction` and the actions column; same `header`/`app-list-state`/`mat-paginator` order and the same `[disabled]` rule on the paginator | agents-list's `emptyAction` has no counterpart in usage, which is correct, but no check asserts it is absent (same gap as the primary action above) |
| `src/web/src/app/shared/list-state.ts` | yes - read in full | none - `list-loading`, `list-empty` with `emptyMessage`, `list-error-title` = `problem.title`, `list-retry` `Tentar de novo` | - (C26, C27, C28) |
| `src/web/src/app/shell/shell.ts` | yes - read in full | none - `Uso` link `data-testid="nav-ai-usage"`, `routerLink="/ai/usage"`, inside `@if (ai.available())`, under `*appHasPermission="permissions.agentRead"`, placed right after `Agentes` (`shell.ts:64-70`) | Two things the plan's Observable row "navegação (ordem e membro novo)" decides have no check: the position of the item (after `Agentes`, last in the AI group) and the item hidden without `ai.agent.read` (`shell.spec.ts:124-133` exists but no check names it) |
| OpenTelemetry GenAI semconv (gen-ai-spans v1.41.0, gen-ai-agent-spans main) | yes - fetched both. Execute-tool span from `gen-ai-spans.md`, invoke_agent internal/client variants from `gen-ai-agent-spans.md` | The inference (`chat`) span lists `gen_ai.provider.name` as **Required**. `AgentLoop.cs:86-88` sets only `gen_ai.operation.name` and `gen_ai.request.model`, plus the tokens at `:92-93`. The plan's AC 11 and check C11 inherit this omission. Everything else matches: span names `invoke_agent {agent.name}`, `chat {request.model}`, `execute_tool {tool.name}`. Keys `gen_ai.operation.name`, `gen_ai.provider.name`, `gen_ai.request.model`, `gen_ai.agent.id`, `gen_ai.agent.name`, `gen_ai.conversation.id`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.tool.name`, `gen_ai.tool.call.id`, `error.type`. Kinds: chat CLIENT, execute_tool INTERNAL, invoke_agent INTERNAL (the in-process variant). `openrouter` and `microsoft.agent_framework` are not well-known values, which the convention allows for custom systems (Landing door 5) | `gen_ai.provider.name` on the chat span - absent from code, plan and checks |

## Checks

Proof runs, all at `96a9989`:

- Api.Tests: one `dotnet test tests/Api.Tests --filter` with 20 names OR-ed together, trx logger. 21 of 21 results passed (C13 is a 2-row theory). Every name is listed in the trx.
- E2ETests: `dotnet test tests/E2ETests --filter FullyQualifiedName~AiTelemetryE2ETests.InvokeAgentSpan_ShouldShareTraceId_WithAspNetCoreRequestSpan`. 1 of 1 passed.
- Front: one `ng test --no-watch` call with 4 `--include` flags and one `--filter` alternation, run under node 24.15.0. 15 passed, 23 skipped by the filter. Every named test shows a check mark in the verbose reporter.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C10 | `invoke_agent {agent}` span with operation, provider, model, agent id/name | Api.Tests batch - `Handle_ShouldStartInvokeAgentSpan_WithGenAiAttributes` Passed | `tests/Api.Tests/Ai/AgentTelemetryTests.cs:35` `Assert.Equal("invoke_agent Default", span.DisplayName)` · `:36-40` operation `invoke_agent`, provider `stub`, model `stub`, `agent.Id.ToString()`, `Default`. Precision gap: the test config uses `stub` for both the provider label and `Ai:Llm:Model` (`TestServiceFactory.cs:39`), so a provider/model swap would pass | PASS |
| C11 | one `chat {model}` span per LLM call, summary call included, with tokens | batch - `RunAsync_ShouldStartChatSpan_PerLlmCall_IncludingSummaryFallback` Passed | `AgentTelemetryTests.cs:70` `Assert.Equal(6, chats.Count)` · `:73` `"chat m1"` · `:77` `5 == chats.Count(input_tokens == 3)` · `:78-79` summary `input_tokens 7`, `output_tokens 2`. Parentage is asserted only as "same trace as the capture root" (`:257`) | PASS |
| C12 | `execute_tool {tool}` span with operation, tool name, call id | batch - `ExecuteAsync_ShouldStartExecuteToolSpan_WithGenAiAttributes` Passed | `AgentTelemetryTests.cs:90` `"execute_tool t"` · `:91-93` `execute_tool`, `t`, `c1` | PASS |
| C13 | outside allowlist or unknown tool -> `error.type = tool_not_found`, loop continues | batch - both theory rows Passed | `AgentTelemetryTests.cs:109` `Assert.Equal("tool_not_found", span.GetTagItem("error.type"))` · `:110` `Assert.Equal("done", result.Reply)` · `:111` tool message contains `tool_not_found` | PASS |
| C14 | LLM or tool exception (which the loop turns into `tool_failed`/`permission_denied`) -> span `error.type` = exception type, status Error | batch - both named proofs Passed | `AgentTelemetryTests.cs:122-123` `"HttpRequestException"`, `ActivityStatusCode.Error` (chat) · `:135-136` `"InvalidOperationException"`, `Error` (tool, `tool_failed` path). The `permission_denied` path the claim names (`AgentLoop.cs:128-130`) has no proof, and fault 3 survived | FAIL |
| C15 | chat failing for any reason -> `invoke_agent` `error.type` | batch - `InvokeAgentSpan_ShouldSetErrorType_WhenChatFails` Passed | `AgentTelemetryTests.cs:148-149` `"HttpRequestException"`, `ActivityStatusCode.Error`. Proven on one cause; see Coverage | PASS |
| C16 | none of 4 content attributes on any span | batch - `Spans_ShouldNotContain_ContentAttributes` Passed | `AgentTelemetryTests.cs:163` `Assert.Empty(span.TagObjects.Select(t => t.Key).Intersect(forbidden))` over all 4 · `:161` an `execute_tool` span is present · `:164` no tag value contains the user message | PASS |
| C17 | traces on -> `invoke_agent` child of the ASP.NET Core span, same TraceId | E2ETests - Passed | `tests/E2ETests/Ai/AiTelemetryE2ETests.cs:42` `Assert.Single(stopped, a => a.Source.Name == "Microsoft.AspNetCore" && a.SpanId == invoke.ParentSpanId)` · `:43` same `TraceId` · `:44` `url.path`. The test's own `ActivityListener` (`:19-25`) listens to both sources, so the result does not depend on `EnableTraces` or `AddSource`: fault 1 left this test green | PASS |
| C18 | traces off -> no `TracerProvider`, Ai source has no listeners | batch - `ActivitySource_ShouldHaveNoListeners_WhenTracesDisabled` Passed | positive control `AgentTelemetryTests.cs:173-174` `NotNull(TracerProvider)`, `True(HasListeners())` with `UseSetting("OpenTelemetry:EnableTraces","true")` (`:194`) · `:180-181` `Null(TracerProvider)`, `False(HasListeners())` · `:186` still no listeners after a 200 chat. Robustness: `HasListeners` is process-wide, but a stray listener can only turn this red, never falsely green. The only listeners on `Api.Features.Ai` in Api.Tests are `SpanCapture` in the same non-parallel collection (`:14`), and no other Api.Tests code turns traces on (grep of `EnableTraces`). Killed fault 1 | PASS |
| C19 | traces off -> chat still `200` with conversationId, reply, iterationsUsed | batch - `ChatAiTests.ChatAi_ShouldReturn200WithReplyAndIterationsUsed_WhenTracesDisabled` Passed | `tests/Api.Tests/Ai/ChatAiTests.cs:359` `HttpStatusCode.OK` · `:361-363` `"olá"`, `1`, `NotEqual(Guid.Empty, ConversationId)`. Flag set with `UseSetting` at `:352`, where it takes effect. `false` is also the default, so this proof cannot tell the flag from the default | PASS |
| C34 | successful chat -> `gen_ai.conversation.id` = returned conversationId, including on create | batch - `InvokeAgentSpan_ShouldCarryConversationId` Passed | `AgentTelemetryTests.cs:53-54` 2 spans, both `== created.ConversationId.ToString()` (the first span is the create) · `:55`. Killed fault 4 | PASS |
| C20 | `GET /ai/usage` 200, rows aggregated per agent with 8 fields | batch - both proofs Passed | handler `tests/Api.Tests/Ai/GetAiUsageTests.cs:35` `("Alpha", 2, 1, 13L, 6L, 19L, Day.AddHours(1))` · `:37` Beta tuple · `:38` `TotalCount == 2` (other-tenant row excluded) · HTTP `:93` `OK` · `:96-98` sorted key set of the 8 fields | PASS |
| C21 | `GET /api/v1/ai/usage` with no params: `totalTokens` desc, stable by `agentId`, page 1/20 | batch - Passed | `GetAiUsageTests.cs:54` order `[ids[1], ties...]` · `:55` `(1, 20)`. Handler only, called with `new GetAiUsageQuery()` defaults. The claim names the route, but the endpoint's own defaults `pageNumber ?? 1`, `pageSize ?? 20` (`src/Api/Features/Ai/GetAiUsage.cs:75`) have no HTTP proof: a level gap | FAIL |
| C22 | only rows with `createdAt` inside `from`/`to`, both ends included | batch - Passed | `GetAiUsageTests.cs:72` `Assert.Equal((2, 110L), (row.Calls, row.InputTokens))` with rows at -1 tick, both bounds, +1 tick. Handler level. The endpoint's `DateTimeOffset -> UtcDateTime` conversion (build note 4) has no proof | PASS |
| C23 | `from > to` -> 400 `Validation failed` | batch - both Passed | `GetAiUsageTests.cs:80` `ShouldHaveValidationErrorFor(x => x.From)` · `:81` equal bounds valid · `:109-110` `BadRequest`, `"Validation failed"` | PASS |
| C24 | authenticated without `ai.agent.read` -> 403 | batch - Passed | `GetAiUsageTests.cs:121` `HttpStatusCode.Forbidden` | PASS |
| C25 | `EnableAI=false` -> 404 `Feature disabled` | batch - Passed | `GetAiUsageTests.cs:132-133` `NotFound`, `"Feature disabled"` | PASS |
| C26 | empty -> `Sem utilização registada` | front batch - `Usage > estado vazio` passed | `src/web/src/app/features/ai/usage.spec.ts:22` `expect(text(fixture, 'list-empty')).toBe('Sem utilização registada')` | PASS |
| C27 | loading -> `list-loading` and the paginator disabled | front batch - `estado de carregamento: 'usage'` passed | `src/web/src/app/shared/list-state.spec.ts:77` `list-loading` not null · `:78` paginator `disabled` `toBe(true)` | PASS |
| C28 | 500 -> ProblemDetails title + `Tentar de novo` | front batch - `estado de erro repete a query: 'usage'` passed | `list-state.spec.ts:117` `toBe('Unexpected error')` · `:118` `toBe('Tentar de novo')` · `:123` retry refetches | PASS |
| C29 | no `ai.agent.read` -> `forbidden` screen with its copy | front batch - passed | `usage.spec.ts:77` `expect(router.url).toBe('/forbidden')` · `:80` `toContain('Sem permissão para esta operação')` | PASS |
| C30 | nav `Uso` visible with permission + flag, hidden when the flag answers 404 | front batch - both passed | `src/web/src/app/shell/shell.spec.ts:108` `toBe('Uso')` · `:109` href `/ai/usage` · `:121` `toBeNull()` after `AiAvailability.disable()`. The hidden case calls `disable()` directly instead of going through the 404, the same pattern as the existing Agentes test | PASS |
| C31 | usage client as a flat file, no layer folders | front batch - both passed | `src/web/src/app/architecture.spec.ts:52` `expect(missing).toEqual([])` · `:87` `expect(walk(featuresRoot)).toEqual([])` | PASS |
| C33 | unauthenticated -> 401 | batch - Passed | `GetAiUsageTests.cs:145` `HttpStatusCode.Unauthorized` | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `GET /api/v1/ai/usage` statuses (5) | plan Surface | 200 C20 · 400 C23 · 401 C33 · 403 C24 · 404 C25, all over HTTP | - |
| `GET /api/v1/ai/usage` query inputs (4) | plan Surface `In` + `GetAiUsage.cs:67-75` | `from`/`to` bound over HTTP C23 · range semantics C22 (handler) | `pageNumber`/`pageSize` defaults at the route (`GetAiUsage.cs:75`) have no HTTP proof |
| GenAI operation names (3) | Landing door 5 + semconv | `invoke_agent` C10 · `chat` C11 · `execute_tool` C12 | - |
| `LlmRequest` sites in `AgentLoop` (2) | code `AgentLoop.cs:35`, `:71` (only `llm.CompleteAsync` at `:91`) | iteration C11 · summary C11 (fault 2 killed) | - |
| tool execution paths in `AgentLoop` (1 site, 5 outcomes) | code `AgentLoop.cs:61` -> `ExecuteToolAsync` `:105-148` | span on every path, opened at `:109` before any branch · ok C12 · tool_not_found C13 · generic exception C14 | `permission_denied` (`:128-130`, fault 3 survived) · cancellation `:123-126` |
| `invoke_agent` attributes (6) | Landing door 5 + rebase | operation, provider, model, agent.id, agent.name C10 · conversation.id C34 | - |
| semconv Required attributes per span | semconv gen-ai-spans / agent-spans | invoke_agent `operation.name`, `provider.name` C10 · execute_tool `operation.name`, `tool.name` C12 · chat `operation.name` C11 | chat `gen_ai.provider.name` - not emitted (`AgentLoop.cs:86-88`) |
| `gen_ai.provider.name` values (3) | Landing door 5, `AiTelemetry.cs:33-38` | `stub` C10 | `openrouter` · `microsoft.agent_framework` |
| span `error.type` outcomes (3) | checks + code | `tool_not_found` C13 · exception type on own span C14 (chat, tool_failed) · exception type on `invoke_agent` C15 | exception type on the `permission_denied` path |
| `invoke_agent` failure causes (C15 "qualquer razão") | `ChatAi.cs:64-80` + exits in `RunTurnAsync` | LLM exception C15 | agent not found 404 · 409 (agent mismatch / MaxItems) · quota 429 · content blocked 400 |
| content attributes excluded (4) | AC 16 | C16, over all 4 | - |
| one-way doors (4) | plan Landing 3, 4, 5, 7 | tokens in `LlmResponse` C11 · `AddSource` C18 positive control (fault 1 killed) · vocabulary C10-C12, C34 · read contract C20 | - |
| Relations entities (1) | plan Relations | `AiUsageEntry` read-only C20 (tenant excluded at `GetAiUsageTests.cs:30`, `:38`) | - |
| screen `usage` states (4) | plan Observable | empty C26 · loading C27 · error C28 · forbidden C29 | - |
| screen `usage` arrangement and copy (6) | binding: plan S3 + `agents-list.ts` | none named by a check | h1 `Uso do AI` · 7 columns and their order · no search · no sort headers · no primary action · paginator outside `app-list-state` |
| shell nav item `Uso` (4) | `shell.ts:64-70` + plan Observable | visible C30 · hidden when the flag is off C30 | hidden without `ai.agent.read` (test exists, no check) · position after `Agentes` |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, crossed by a boundary | `GetAiUsage.cs` (handler, validator), `AiUsageEntry.cs` `SummarizeByAgentAsync` | boundary C20, C23 · own level C20, C21, C22, C23 | no - the route's pagination defaults (`GetAiUsage.cs:75`) have no proof at the boundary, so "the contract at the boundary" is short one member |
| Decide, not crossed by a boundary | `AgentLoop.cs` (error.type), `ChatAi.cs` (invoke_agent error), `usage.ts` (screen state) | own level: C13, C14, C15 · C26-C29 | no - decision row `permission_denied -> error.type` has no case (fault 3 survived). `AiTelemetry.ProviderValue` is a 3-arm decision filed as instrumentation, with 1 arm proven |
| Entry point that does not decide | `GetAiUsageEndpoint` | boundary: accepted C20 · rejected C23 · 401 C33 · 403 C24 · 404 C25 | yes |
| Instrumentation, pass-through | `AiTelemetry.cs` constants, `ObservabilityConfiguration.cs:28-30` | none of its own; consumer proofs | yes - C18 positive control kills a missing `AddSource` |

Swept rows re-read against the code:

- The tenant filter cited as `AiUsageTests.UsageEntries_ShouldBeTenantFiltered` exists (`AiUsageTests.cs:177`).
- `GET /ai/usage` sits outside the `ai` rate-limit policy: there is no `RequireRateLimiting` in `GetAiUsage.cs:79-87`.
- The append-only ledger holds: `IAiUsageRepository` has no update or delete method (`AiUsageTests.cs:169`, now listing `SummarizeByAgentAsync`).

One self-report in `checks.md` Coverage does not hold. The note claims every route, status or shape check "tem uma prova que atravessa a fronteira HTTP", but C21 and C22 have handler proofs only.

## Faults injected

Scratch worktree `scratchpad/verify-w1` at `96a9989`. Real-tree `git status --porcelain` was empty before and after, and the two captures diff clean. The worktree was removed and `git worktree list` shows only the main tree.

| Mutation | Location | Killed |
| --- | --- | --- |
| drop `.AddSource(AiTelemetry.ActivitySourceName)` | `src/Api/Host/Configurations/ObservabilityConfiguration.cs:30` | yes - C18 `Assert.True() Failure` (`:174`); C17 stayed green under this mutant |
| summary call goes to `llm.CompleteAsync` directly (no chat span) | `src/Api/Features/Ai/AgentLoop.cs:70` | yes - C11 `Expected: 6 Actual: 5` |
| remove `RecordError` in the `UnauthorizedAccessException` (permission_denied) catch | `src/Api/Features/Ai/AgentLoop.cs:130` | no - AgentTelemetryTests + AgentLoopGuardrailTests 28/28 green |
| remove the `gen_ai.conversation.id` tag | `src/Api/Features/Ai/ChatAi.cs:72` | yes - C34 `Assert.All() Failure: 2 out of 2` |
| `to` bound `<=` -> `<` | `src/Api/Features/Ai/AiUsageEntry.cs:91` | yes - C22 `Expected (2, 110) Actual (1, 10)` |

## Gate

- `dotnet test tests/Api.Tests` (full): 328 passed, 0 failed.
- `dotnet test tests/ArchitectureTests`: 18 passed, 0 failed.
- `dotnet test tests/E2ETests --filter ...AiTelemetryE2ETests...`: 1 passed.
- Front batch: 15 passed, 0 failed.

The suite is green. The verdict is FAIL on these findings:

- C14 and C21 fail.
- One mutant survived.
- One semconv contradiction.
- Screen arrangement and nav elements have no checks.
- 9 coverage members are unproven.
- 2 unmet Test policy rows.
