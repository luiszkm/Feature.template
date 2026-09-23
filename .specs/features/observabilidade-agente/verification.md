# Observabilidade do loop do agente (W1) verification

**Verdict**: PASS
**Profile**: ui
**Diff range**: dc21725..1e2ae6d (fix range 96a9989..1e2ae6d)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Round 1 (at `96a9989`) was FAIL. This round is scoped per verify.md "Re-verifying after a fix":
- **Re-run in full at `1e2ae6d`:** every proof of the 26 checks (C10–C31, C33–C36).
- **Re-injected faults:** on the surfaces the fix touched or created.
- **Recomputed coverage rows:** the ones the fix touched.
- **Test policy:** both unmet rows re-judged.
- **Binding sources:** re-opened only for the usage screen and the nav.

Everything else is marked `carried from 96a9989`. In the fix, the only code change is `AgentLoop.cs` (a new optional `IHostEnvironment`, and the provider tag on the chat span). The other changed files are tests, `checks.md` and `STATE.md`. `usage.ts`, `shell.ts` and `ChatAi.cs` are unchanged since `96a9989`.

**Superseded S1 (C1–C9, C32)** is carried from 96a9989. Two superseded behaviours have no test in `AiUsageTests.cs`:
- C9: two identical executions record two rows.
- C5: the `Development` trigger with no ApiKey.

Both are now recorded as a `comparar-modelos` finding in `.specs/STATE.md:85-89`. Neither is this feature's.

**Step 5** (walk the flow with the user) cannot run from a sub-agent. The usage screen and the nav item have not been walked with a person.

## Binding sources

Verified at 1e2ae6d for the usage screen and the nav. The rows for `list-state.ts` and the semconv are carried from 96a9989. The semconv contradiction found there is closed by C36 and was re-checked at this commit.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| plan S3 arrangement paragraph + checks.md C35 | yes - `plan.md` S3; `usage.ts:35-91` re-read (unchanged since 96a9989) | none - C35 covers the layout: `usage.spec.ts:91` root children are `header`, `app-list-state`, `mat-paginator` (3 regions in that order, paginator outside the list state) · `:97` header children are only `h1` (no primary action) · `:98` `Uso do AI` · `:99` no `mat-form-field` · `:55` 7 columns in order · `:65` no `search` · `:66` no `mat-sort-header` | - |
| `src/web/src/app/features/ai/agents-list.ts` | yes - re-read | none - the empty-state `emptyAction` that agents-list has is proven absent by C26's exact text match: `usage.spec.ts:22` `toBe('Sem utilização registada')` on `textContent` of `list-empty` (`src/web/src/testing.ts:27`). A projected action would change the text | - |
| `src/web/src/app/shared/list-state.ts` | carried from 96a9989 | none - covered by C26, C27, C28 | - |
| `src/web/src/app/shell/shell.ts` | yes - `shell.ts:64-70` re-read (unchanged) | none - covered by C30: visible `shell.spec.ts:108-109` · hidden by the flag through `learnFrom(404, 'Feature disabled')` `:122` · hidden without `ai.agent.read` `:133` · last after `Agentes` `:146` `order.slice(-4)` = `nav-ai, nav-conversations, nav-agents, nav-ai-usage` | - |
| OpenTelemetry GenAI semconv (gen-ai-spans v1.41.0, gen-ai-agent-spans main) | carried from 96a9989 (fetched there); the chat-span row re-checked against code at 1e2ae6d | none - `gen_ai.provider.name`, which the convention marks Required on inference spans, is now set on the chat span (`src/Api/Features/Ai/AgentLoop.cs:90-91`) | - |

## Checks

Proof runs, all at `1e2ae6d`:

- **Api.Tests:** one `dotnet test tests/Api.Tests --filter` with 26 names OR-ed together, trx logger. 31 of 31 results passed. Each of the 26 names appears individually in the trx, with theories at 3/2/3 rows.
- **E2ETests:** `...AiTelemetryE2ETests.InvokeAgentSpan_ShouldShareTraceId_WithAspNetCoreRequestSpan`, 1 of 1 passed.
- **Front:** one `ng test --no-watch` call in the real tree, run under node 24.15.0, with 4 `--include` flags and one `--filter` alternation of 12 names. 19 passed and 21 were skipped by the filter. Each named test shows a check mark in the verbose reporter.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C10 | `invoke_agent {agent}` with operation, provider, model, agent id/name | Api batch - `Handle_ShouldStartInvokeAgentSpan_WithGenAiAttributes`, `ProviderValue_ShouldMapEveryProviderLabel` (3 rows) Passed | `tests/Api.Tests/Ai/AgentTelemetryTests.cs:38-43` Default span `invoke_agent`, `stub`, `stub`, agent id, `Default` · `:46-47` agent with its own model: `request.model == StubModelCatalog.ModelA` while `provider.name == "stub"`, so model and provider now differ · `:181` `Assert.Equal(expected, AiTelemetry.ProviderValue(label))` over `openrouter`, `microsoft.agent_framework`, `stub` | PASS |
| C11 | one `chat {model}` span per LLM call, summary included, tokens | Api batch - Passed | `AgentTelemetryTests.cs:78` `Assert.Equal(6, chats.Count)` · `:81-83` `chat m1` · `:85` 5 × `input_tokens 3` · `:86-87` summary `7`/`2`. Parent is `invoke_agent`: `tests/E2ETests/Ai/AiTelemetryE2ETests.cs:38` `Assert.Equal(invoke.SpanId, chat.ParentSpanId)` | PASS |
| C12 | `execute_tool {tool}` with operation, tool name, call id | Api batch - Passed | `AgentTelemetryTests.cs:98-101` `execute_tool t`, `execute_tool`, `t`, `c1` | PASS |
| C13 | outside allowlist or unknown -> `tool_not_found`, loop continues | Api batch - 2 rows Passed | `AgentTelemetryTests.cs:117` `"tool_not_found"` · `:118` `"done"` · `:119` tool message contains `tool_not_found` | PASS |
| C14 | LLM or tool exception (`tool_failed` or `permission_denied`) -> `error.type` = type name, status Error | Api batch - 3 proofs Passed | chat `AgentTelemetryTests.cs:130-131` `HttpRequestException`, `Error` · tool_failed `:143-144` `InvalidOperationException`, `Error` · permission_denied `:216-217` `Assert.Equal("UnauthorizedAccessException", span.GetTagItem("error.type"))`, `ActivityStatusCode.Error` · `:218` loop continues, `"done"`. Round-1 survivor now killed (fault 1) | PASS |
| C15 | chat failing for any reason -> `invoke_agent` `error.type` | Api batch - 2 proofs Passed | `AgentTelemetryTests.cs:156-157` `HttpRequestException`, `Error` · `:249-251` the Error spans' `error.type`, sorted, equals `BusinessRuleException, ContentBlockedException, NotFoundException, TooManyRequestsException`, each thrown at `:233-242`. Killed fault 4 | PASS |
| C16 | no content attributes on any span | Api batch - Passed | `AgentTelemetryTests.cs:171` `Assert.Empty(...Intersect(forbidden))` over all 4 · `:172` no tag value holds the message | PASS |
| C17 | traces on -> `invoke_agent` child of the ASP.NET Core span, same TraceId | E2E - Passed | `AiTelemetryE2ETests.cs:35` same `TraceId` · `:36` `Assert.Equal(onRequest.SpanId, invoke.ParentSpanId)` · `:45` traces off: `DoesNotContain(off, Ai source)`. The test's listener returns `None` from `Sample` for the Ai source (`:24-27`), so Ai spans exist only when the app's TracerProvider samples them. Now killed by dropping `AddSource` (fault 2) | PASS |
| C18 | traces off -> no TracerProvider, no listeners | Api batch - Passed | positive control `AgentTelemetryTests.cs:260-261` · `:267-268` `Null(TracerProvider)`, `False(HasListeners())` · `:273`. Killed fault 2 | PASS |
| C19 | traces off -> chat `200` with conversationId, reply, iterationsUsed | Api batch - Passed | `tests/Api.Tests/Ai/ChatAiTests.cs:359` `OK` · `:361-363` `"olá"`, `1`, conversationId not empty (flag via `UseSetting` at `:352`) | PASS |
| C20 | 200, rows aggregated per agent with 8 fields | Api batch - 2 proofs Passed | `tests/Api.Tests/Ai/GetAiUsageTests.cs:35` Alpha tuple · `:37` Beta tuple · `:38` `TotalCount == 2` · HTTP `:93` `OK` · `:96` 8-key set | PASS |
| C21 | `GET /api/v1/ai/usage` without params: `totalTokens` desc, stable by `agentId`, page 1/20 | Api batch - 2 proofs Passed | handler `GetAiUsageTests.cs:54` order · `:55` `(1, 20)` · HTTP `:109` `Assert.Equal(1, ...GetProperty("pageNumber"))` · `:110` `Assert.Equal(20, ...GetProperty("pageSize"))`. The level gap is closed | PASS |
| C22 | only rows with `createdAt` inside `from`/`to`, bounds included | Api batch - 2 proofs Passed | handler `GetAiUsageTests.cs:72` `(2, 110L)` · HTTP `:132` `True(Includes(now-2h, now+1h))` · `:133` `False(Includes(now+1h, now+2h))`, with bounds sent at `+05:00`. Killed fault 5 | PASS |
| C23 | `from > to` -> 400 `Validation failed` | Api batch - 2 proofs Passed | `GetAiUsageTests.cs:80-81` validator · `:144-145` `BadRequest`, `"Validation failed"` | PASS |
| C24 | no `ai.agent.read` -> 403 | Api batch - Passed | `GetAiUsageTests.cs:156` `Forbidden` | PASS |
| C25 | `EnableAI=false` -> 404 `Feature disabled` | Api batch - Passed | `GetAiUsageTests.cs:167-168` `NotFound`, `"Feature disabled"` | PASS |
| C26 | empty -> `Sem utilização registada` | front batch - passed | `src/web/src/app/features/ai/usage.spec.ts:22` `toBe('Sem utilização registada')` | PASS |
| C27 | loading -> `list-loading`, paginator disabled | front batch - `estado de carregamento: 'usage'` passed | `src/web/src/app/shared/list-state.spec.ts:77` not null · `:78` `disabled` `toBe(true)` (file unchanged; lines carried from 96a9989) | PASS |
| C28 | 500 -> title + `Tentar de novo` | front batch - `estado de erro repete a query: 'usage'` passed | `list-state.spec.ts:117` `'Unexpected error'` · `:118` `'Tentar de novo'` | PASS |
| C29 | no permission -> `forbidden` with its copy | front batch - passed | `usage.spec.ts:76` `toBe('/forbidden')` · `:80` `toContain('Sem permissão para esta operação')` | PASS |
| C30 | nav `Uso` visible with permission + flag; hidden when the flag answers 404 | front batch - 4 proofs passed | `src/web/src/app/shell/shell.spec.ts:108-109` `'Uso'`, `/ai/usage` · `:122` null after `learnFrom({status:404,title:'Feature disabled'})` (`:117`) · `:133` null without `ai.agent.read` · `:146` last 4 nav ids end in `nav-ai-usage` after `nav-agents` | PASS |
| C31 | usage client as a flat file, no layer folders | front batch - 2 proofs passed | `src/web/src/app/architecture.spec.ts:52` `missing` `toEqual([])` · `:87` `walk(featuresRoot)` `toEqual([])` | PASS |
| C33 | unauthenticated -> 401 | Api batch - Passed | `GetAiUsageTests.cs:180` `Unauthorized` | PASS |
| C34 | `gen_ai.conversation.id` = returned id, including on create | Api batch - Passed | `AgentTelemetryTests.cs:61-62` 2 spans, both `created.ConversationId` · `:63` | PASS |
| C35 | usage screen: 3 regions in order, header only with `h1` `Uso do AI`, no search, no sort, 7 columns in order | front batch - 2 proofs passed | `usage.spec.ts:91` `['header','app-list-state','mat-paginator']` · `:97` `['h1']` · `:98` `'Uso do AI'` · `:99` no `mat-form-field` · `:55` columns `Agente` .. `Último uso` · `:65-66` no `search`, no `mat-sort-header` | PASS |
| C36 | every `chat` span carries `gen_ai.provider.name` of the active provider, also through the real host | Api batch - 3 rows Passed; E2E Passed | `AgentTelemetryTests.cs:203` `Assert.Equal(expected, span.GetTagItem("gen_ai.provider.name"))` over `openrouter` (Production + key), `microsoft.agent_framework`, `stub` (Testing) · `:204` model `m` · host `AiTelemetryE2ETests.cs:39` `Assert.Equal("stub", chat.GetTagItem(AiTelemetry.ProviderName))`. Killed fault 3 | PASS |

## Coverage

Rows the fix touched were recomputed at 1e2ae6d. The rest are carried from 96a9989.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `GET /api/v1/ai/usage` statuses (5) | carried from 96a9989 - plan Surface | 200 C20 · 400 C23 · 401 C33 · 403 C24 · 404 C25 | - |
| `GET /api/v1/ai/usage` query inputs (4) | plan Surface `In` + `GetAiUsage.cs:67-75` | `from`/`to` as UTC instants over HTTP C22 (`:132-133`) · `pageNumber`/`pageSize` defaults over HTTP C21 (`:109-110`) | - |
| GenAI operation names (3) | carried from 96a9989 | `invoke_agent` C10 · `chat` C11 · `execute_tool` C12 | - |
| `LlmRequest` sites in `AgentLoop` (2) | code `AgentLoop.cs:36`, `:72` (only `llm.CompleteAsync` inside `CompleteAsync`) | iteration C11 · summary C11 | - |
| tool execution paths in `AgentLoop` (5 outcomes, 1 site) | code `AgentLoop.cs:62` -> `ExecuteToolAsync`, span opened at `:112` before any branch | ok C12 · tool_not_found C13 · permission_denied C14 · other exception C14 · cancellation (`OperationCanceledException`, rethrown, not turned into a tool error) is outside every claim: n/a, no proof owed | - |
| semconv Required attributes per span | semconv gen-ai-spans / agent-spans | invoke_agent `operation.name`, `provider.name` C10 · chat `operation.name` C11, `provider.name` C36 · execute_tool `operation.name`, `tool.name` C12 | - |
| `gen_ai.provider.name` values (3) | Landing door 5, `AiTelemetry.cs:33-38` | `openrouter`, `microsoft.agent_framework`, `stub` - own level C10 (`:181`), through the resolver C36 (`:203`), host C36 (E2E `:39`) | - |
| chat attributes (5) | AC 11 + semconv | operation, model, input and output tokens C11 · provider C36 | - |
| `invoke_agent` failure causes (5) | `ChatAi.cs:64-80` + the exits in `RunTurnAsync` | LLM exception C15 · NotFound, BusinessRule, TooManyRequests, ContentBlocked C15 (`:249-251`) | - |
| `invoke_agent` attributes (6) | carried from 96a9989 | C10 × 5 · conversation.id C34 | - |
| content attributes excluded (4) | carried from 96a9989 | C16 over all 4 | - |
| one-way doors (4) | carried from 96a9989 | C11 · C18 · C10-C12, C34, C36 · C20 | - |
| Relations entities (1) | carried from 96a9989 | `AiUsageEntry` C20 | - |
| screen `usage` states (4) | carried from 96a9989 | C26 · C27 · C28 · C29 | - |
| screen `usage` arrangement and copy (6) | binding: plan S3 + `agents-list.ts` | regions and order C35 · h1 copy C35 · no primary action C35 (header only `h1`) · no search C35 · no sort C35 · 7 columns in order C35 | - |
| shell nav item `Uso` (4) | `shell.ts:64-70` + plan Observable | visible C30 · hidden by the flag via 404 C30 · hidden without permission C30 · after `Agentes` C30 | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, crossed by a boundary | `GetAiUsage.cs`, `AiUsageEntry.cs` `SummarizeByAgentAsync` | boundary C20, C21 (`:109-110`), C22 (`:132-133`), C23 · own level C20-C23 | yes - verified at 1e2ae6d: the route's pagination defaults and range conversion now have HTTP proofs |
| Decide, not crossed by a boundary | `AgentLoop.cs` (error.type), `ChatAi.cs` (invoke_agent error), `AiTelemetry.ProviderValue`, `usage.ts` (screen state) | own level: C13, C14, C15, C10 `ProviderValue` theory, C26-C29 | yes - verified at 1e2ae6d. The permission_denied row has a case (`:216`). `ProviderValue` is now proven as a decision: each of its 3 arms is asserted at its own level (`:181`), and through `LlmServiceResolver` into the span (`:203`) |
| Entry point that does not decide | `GetAiUsageEndpoint` | boundary: C20 · C23 · C33 · C24 · C25 | yes - carried from 96a9989 |
| Instrumentation, pass-through | `AiTelemetry.cs` constants, `ObservabilityConfiguration.cs:28-30` | the consumers' proofs | yes - a missing `AddSource` is now killed by C18 and C17 |

Swept rows are carried from 96a9989, where they were re-read against the code: tenant filter, no rate limit on `GET /ai/usage`, append-only ledger. The fix touched none of them.

## Faults injected

Verified at 1e2ae6d, in the scratch worktree `scratchpad/verify-w1` (detached at `1e2ae6d`). The real tree's `git status --porcelain` was empty before and after, and the two captures diff clean. The worktree was removed; `git worktree list` shows only the main tree.

| Mutation | Location | Killed |
| --- | --- | --- |
| remove `RecordError` in the `UnauthorizedAccessException` (permission_denied) catch - the round-1 escapee | `src/Api/Features/Ai/AgentLoop.cs:133` | yes - C14 `ExecuteToolSpan_ShouldSetErrorType_WhenToolDeniesPermission`, `Expected: UnauthorizedAccessException Actual: null` |
| drop `.AddSource(AiTelemetry.ActivitySourceName)` | `src/Api/Host/Configurations/ObservabilityConfiguration.cs:30` | yes - C17 E2E `Assert.Single() Failure` (the round-1 C17 test stayed green here); C18 `Assert.True() Failure` |
| remove the `gen_ai.provider.name` tag on the chat span | `src/Api/Features/Ai/AgentLoop.cs:91` | yes - C36 theory 3/3 failed; E2E `Expected: stub Actual: null` |
| invoke_agent skips `RecordError` for `NotFoundException` (`catch ... when (ex is not NotFoundException)`) | `src/Api/Features/Ai/ChatAi.cs:75` | yes - C15 `ForEveryWayTheChatFails`: the collection has no `NotFoundException` |
| endpoint passes `from?.DateTime` instead of `UtcDateTime` | `src/Api/Features/Ai/GetAiUsage.cs:75` | yes - C22 `Get_ShouldReadOffsetRange_AsUtcInstants_OverHttp` `Assert.True() Failure` |

## Gate

- `dotnet test tests/Api.Tests` (full, at 1e2ae6d): 338 passed, 0 failed.
- `dotnet test tests/ArchitectureTests`: 18 passed, 0 failed.
- E2E proof: 1 passed.
- Front batch: 19 passed, 0 failed.

Residual notes. None of these fails the gate:

1. **`ProviderValue`'s fallback arm.** `_ => providerLabel.ToLowerInvariant()` is only exercised with `stub`, which is already lowercase. The lowercasing itself does not discriminate. It is unreachable in practice, because `LlmServiceResolver.ProviderLabel` returns only `stub` or a normalized known label.
2. **C17 flake risk.** The E2E test relies on the process-wide TracerProvider. A future parallel E2E class that calls the chat or comparisons routes during the traces-on window could make `Assert.Single` flaky. That would be a false red, never a false green.
3. **Stale count in `checks.md`.** The Intent line says "24 checks", but the file has 26 (C10–C31, C33–C36).
4. **Step 5 not run.** It needs a person.
