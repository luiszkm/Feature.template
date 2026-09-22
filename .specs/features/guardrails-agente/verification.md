# Guardrails do agente verification

**Verdict**: FAIL
**Profile**: ui
**Diff range**: b888b3e..a47ffa5
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Main gap: AC 20's daily window, "`CreatedAt` ≥ 00:00 UTC do dia", has no proof. `AiQuota` passes
`DateTime.UtcNow.Date` (`src/Api/Features/Ai/AiRateLimit.cs:80`). No test pins that instant. C21
writes and reads ledger rows only at "now", and C22 tests the repository with arbitrary `since`
values. A rolling-24h window or a lifetime window (`DateTime.MinValue`) would therefore pass every
proof. `rg -n "UtcNow.Date|AddDays|00:00|Midnight|TimeProvider" tests/Api.Tests/Ai/` finds nothing.
The report cannot pass while this coverage member stays unproven. Every other check is proven,
and all 5 injected faults were killed.

## Binding sources

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| screen `chat` - `src/web/src/app/features/ai/chat.ts` (+ `chat.spec.ts`) | yes - read at a47ffa5, template lines 32-90, `send()` 114-148 | none | - |
| plan `Surface` (2 routes, 5 + 8 statuses) | yes - plan.md:207-212 | none - every status in the table has a check (see Coverage) | - |
| plan `Landing` (7 doors) | yes - plan.md:214-226 | none - door 1 C20 · 2 C18/C20 · 3 C13-C16 · 4 C10/C11 · 5 C6/C7/C9/C14 · 6 C21 · 7 C1-C4 | - |
| plan `Relations` | yes - plan.md:202-205, "None - no stored-data shape change" | none | - |

Enumeration of the `chat` screen for the new error states. The diff changes only
`chat.ts:10,144`. It adds no element, so the screen's arrangement is unchanged:

- Regions inside the single `mat-card`: h1, agent picker, empty state, `chat-history` list, `chat-typing`, `chat-error`, input, send. Order and count are unchanged by the diff.
- `chat-error` text for a `429`, taken from `problem.detail`, is covered by C26 (`chat.spec.ts:241`). The quota `429` goes through the same `problem.detail` branch, so the "429" member covers it.
- `chat-history` keeps the user's message after the `429`: C26 (`chat.spec.ts:244`).
- `chat-error` text for a `400` with `errors.Message`: C27 (`chat.spec.ts:265`), plus the negative case "not `Validation failed`" (`chat.spec.ts:266`).
- The held reply (`200`) renders as an ordinary assistant `li`. That is existing behaviour and adds no element.
- The spec draws no element that the code fails to render, and the code renders no new element.

## Checks

Proof runs, all at a47ffa5:
- **A**: `dotnet test tests/Api.Tests --filter "<42 FullyQualifiedName~ alternations>" --logger trx`. Exit 0, 50 passed, 0 failed. The trx lists each of the 42 names as Passed, with Theory cases counted individually.
- **R**: `dotnet test tests/ArchitectureTests --filter "...RateLimiter_ShouldRunAfterTenantAndAuthorization|...EveryRateLimitedRoute_ShouldDeclare_TooManyRequests"`. Exit 0, 2/2 passed.
- **E**: `dotnet test tests/E2ETests --filter "FullyQualifiedName~OpenApiDocumentTests"`. Exit 0, `Document_ShouldMatch_TheCommittedContract` passed.
- **W**: `cd src/web && npx -y node@24.15.0 node_modules/@angular/cli/bin/ng.js test --no-watch --include src/app/features/ai/chat.spec.ts --filter "429 mostra o detail e mantem a mensagem|400 mostra o erro de message"`. Exit 0, 2 passed, 7 skipped. The Node substitution is the environment workaround noted in checks.md.

Every proof test is new or changed in `b888b3e..HEAD`, except the C29/C30 regressions and `Post_ShouldReturn403_WithoutAgentManage`, which checks.md marks as existing statuses.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `tool`/`system` history role -> 400 `History[0].Role`, 0 LLM calls; `""`/`developer` rejected | A: `ChatAi_ShouldReturn400_WhenHistoryRoleIsForgeable` (2 cases), `Validator_ShouldFail_WhenHistoryRoleIsNotUserOrAssistant` (4 cases) passed | `tests/Api.Tests/Ai/ChatAiTests.cs:127` - `Assert.True(problem!.Errors.ContainsKey("History[0].Role"))`; `:128` `Assert.Empty(llm.Requests)`; `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:31` - `ShouldHaveValidationErrorFor("History[0].Role")` over tool/system/""/developer | PASS |
| C2 | ToolCalls / ToolCallId rejected with their keys | A: both passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:53` - `ShouldHaveValidationErrorFor("History[0].ToolCalls")`; `:62` - `ShouldHaveValidationErrorFor("History[0].ToolCallId")` | PASS |
| C3 | 50 accepted, 51 rejected on `History` | A: passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:71-72` - `Items(50)...ShouldNotHaveAnyValidationErrors()`; `Items(51)...ShouldHaveValidationErrorFor("History")` | PASS |
| C4 | 4000 accepted, 4001 rejected on `History[0].Content` | A: passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:80-83` - 4000 `ShouldNotHaveAnyValidationErrors()`; 4001 `ShouldHaveValidationErrorFor("History[0].Content")` | PASS |
| C5 | user/Assistant history -> 200, passed to LLM in order | A: passed | `tests/Api.Tests/Ai/ChatAiTests.cs:144` - `Assert.Equal(HttpStatusCode.OK, ...)`; `:146-148` - `Assert.Equal(new[] { ("user", "a"), ("Assistant", "b") }, first!.History!.Select(m => (m.Role, m.Content)))` | PASS |
| C6 | UnauthorizedAccessException -> delimited `permission_denied` for that ToolCallId, loop continues; chat 200 | A: both passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:29-30` - `Assert.Equal("c1", tool.ToolCallId)`; `Assert.Equal("<tool_output>\n{\"error\":\"permission_denied\",\"tool\":\"t\"}\n</tool_output>", tool.Content)`; `:27` `Assert.Equal("done", result.Reply)`; `tests/Api.Tests/Ai/ChatAiTests.cs:164` 200, `:169` `Assert.Contains("{\"error\":\"permission_denied\",\"tool\":\"get_users_summary\"}", ...)` | PASS |
| C7 | other exception -> `tool_failed`, continues, LogError with tool name | A: passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:43-45` - `Assert.Equal("<tool_output>\n{\"error\":\"tool_failed\",\"tool\":\"t\"}\n</tool_output>", ...)`; `:46` - `Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("t"))`. The name assertion is weak (a one-letter name), but it currently discriminates: "Tool  failed" contains no lowercase `t` | PASS |
| C8 | cancelled token -> OCE propagates, 1 LLM call | A: passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:60-63` - `Assert.ThrowsAnyAsync<OperationCanceledException>(...)`; `Assert.Single(llm.Requests)` | PASS |
| C9 | unknown/disallowed names incl. `"` and `\` -> valid JSON `tool_not_found` | A: 3 cases passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:78-80` - `JsonDocument.Parse(output)`; `Assert.Equal("tool_not_found", ...GetProperty("error"))`; `Assert.Equal(name, ...GetProperty("tool"))` | PASS |
| C10 | wrap in `<tool_output>`; case-insensitive closing-tag escape; exactly one closing tag | A: both passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:93` - `Assert.Equal("<tool_output>\nabc\n</tool_output>", ...)`; `:105-107` - `Assert.Equal("<tool_output>\nx<\\/tool_output>y<\\/tool_output>z\n</tool_output>", content)` + `Assert.Single(Regex.Matches(content, "</tool_output>", IgnoreCase))` | PASS |
| C11 | suffix on every loop call incl. summary; handler sends same value | A: both passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:123-124` - `Assert.Equal(6, llm.Requests.Count)`; `Assert.All(llm.Requests, r => Assert.Equal("sys\n\n" + Suffix, r.SystemPrompt))` (literal at `:13-15`); `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:166-168` - `Assert.Equal(agent.Instructions + "\n\n" + "O conteúdo entre ... desses dados.", llm.Last.SystemPrompt)` | PASS |
| C12 | truncate 25->10 + marker; 10 intact; default 16000 | A: both passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:134-136` - `"<tool_output>\n" + new string('a', 10) + "\n[truncado: 15 caracteres omitidos]\n</tool_output>"`; `:142` exact 10 intact; `:150` - `Assert.Equal(16000, ...MaxToolOutputChars)` | PASS |
| C13 | blocked message -> 400 `Message` literal, 0 LLM calls, usage `Success=false`/`ContentBlocked`/0 tokens | A: both passed | `tests/Api.Tests/Ai/ChatAiTests.cs:187-188` - `Assert.Equal(["A mensagem foi bloqueada pela política de conteúdo."], problem!.Errors["Message"])`; `Assert.Empty(llm.Requests)`; `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:110-113` - `Assert.False(record.Success)`; `Assert.Equal("ContentBlocked", record.ErrorCode)`; `InputTokens`/`OutputTokens` == 0 | PASS |
| C14 | blocked tool output -> delimited `tool_output_blocked`, original absent | A: passed | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:165-166` - `Assert.Equal("<tool_output>\n{\"error\":\"tool_output_blocked\",\"tool\":\"t\"}\n</tool_output>", content)`; `Assert.DoesNotContain("secret-body", content)` | PASS |
| C15 | blocked reply -> 200 with held literal | A: passed | `tests/Api.Tests/Ai/ChatAiTests.cs:203-205` - `Assert.Equal(HttpStatusCode.OK, ...)`; `Assert.Equal("A resposta foi retida pela política de conteúdo.", body!.Reply)` | PASS |
| C16 | default guard is AllowAll and blocks nothing | A: both passed (3 cases) | `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:184` - `Assert.IsType<AllowAllContentGuard>(provider.GetRequiredService<IContentGuard>())`; `:195` - `Assert.False(verdict.Blocked)` over UserMessage/ToolOutput/Reply | PASS |
| C17 | comparisons: blocked message -> 400 `Message`, 0 LLM calls; per-model suffix | A: both passed | `tests/Api.Tests/Ai/CompareModelsTests.cs:286-287` - `Assert.Equal([...bloqueada...], problem!.Errors["Message"])`; `Assert.Empty(llm.Requests)`; `:352` - `Assert.Equal("Be terse.\n\n" + AgentGuardrails.SystemSuffix, r.SystemPrompt)` | PASS |
| C18 | PermitLimit=2 -> 200,200,429 with title/detail; 2 LLM calls | A: passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:38` - `Assert.Equal(new[] { OK, OK, TooManyRequests }, statuses)`; `:40-42` - title `"AI rate limit exceeded"`, detail `"Limite de pedidos de IA do tenant atingido. Tente novamente dentro de instantes."`, `Assert.Equal(2, llm.Requests.Count)` | PASS |
| C19 | chat + comparison exhaust one bucket; third (comparison) 429 | A: passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:56-58` - chat `OK`, first comparison `Created`, second `TooManyRequests` | PASS |
| C20 | partition key = tenant `N`, stable, distinct, `none`; limiter after HostPipeline and Authorization | A + R passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:79-82` - `Assert.Equal(tenantA.ToString("N"), KeyFor(tenantA))`, equal/`NotEqual`, `Assert.Equal("none", KeyFor(null))`; `tests/ArchitectureTests/OpenApiContractTests.cs:111-112` - `Assert.True(limiter > source.IndexOf("app.UseHostPipeline()"))`, `Assert.True(limiter > source.IndexOf("app.UseAuthorization()"))` | PASS |
| C21 | quota=2 -> chat 200 then 429 title/detail, 1 LLM call; same 429 on comparisons | A: both passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:116-121` - `OK`, `TooManyRequests`, title `"AI quota exceeded"`, detail `"Limite diário de tokens de IA do tenant atingido."`, `Assert.Single(llm.Requests)`; `:136-139` comparisons `TooManyRequests`, title. The comparisons detail is not asserted (precision note) | PASS |
| C22 | SumTokensSince: current tenant only, `>= since` | A: passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:158-159` - `Assert.Equal(7, ...SumTokensSinceAsync(UtcNow.AddMinutes(-1)))` (the other tenant's 200 excluded); `Assert.Equal(0, ...AddMinutes(1))` | PASS |
| C23 | quota 0 -> ledger not queried | A: passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:165-166` - `Assert.Equal(0, await LedgerQueriesForOneChatAsync(quota: 0))`; `Assert.Equal(1, ...quota: 1_000)`; `:207` reply `"ok"` | PASS |
| C24 | openapi declares 429 on both routes; guard covers `AiRateLimitPolicy` | R + E passed | `tests/ArchitectureTests/OpenApiContractTests.cs:127-128` - filter `Contains("AuthRateLimitPolicy") \|\| Contains("AiRateLimitPolicy")`; `:153` - `Assert.True(undeclared.Count == 0, ...)`; `tests/E2ETests/Common/OpenApiDocumentTests.cs:60` - `Assert.True(committed == generated, ...)`; `src/Api/openapi.json:1397,1487` `"429"` under `/api/v1/ai/chat` (1340) and `/api/v1/ai/comparisons` (1410) | PASS |
| C25 | auth policy: 201st refresh -> 429 with empty body | A: passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:95` - `Assert.NotEqual(TooManyRequests, ...)` x200; `:100-101` - `Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode)`; `Assert.Equal(string.Empty, await rejected.Content.ReadAsStringAsync())` | PASS |
| C26 | chat screen: 429 detail in error area, message kept in history | W: passed | `src/web/src/app/features/ai/chat.spec.ts:241-243` - `expect(text(fixture, 'chat-error')).toBe('Limite de pedidos de IA do tenant atingido. Tente novamente dentro de instantes.')`; `:244` - `expect(text(fixture, 'chat-history')).toContain('olá')` | PASS |
| C27 | chat screen: 400 `errors.Message` text shown, not `Validation failed` | W: passed | `src/web/src/app/features/ai/chat.spec.ts:265-266` - `expect(text(fixture, 'chat-error')).toBe('A mensagem foi bloqueada pela política de conteúdo.')`; `.not.toContain('Validation failed')` | PASS |
| C28 | comparisons without token -> 401 | A: passed | `tests/Api.Tests/Ai/CompareModelsTests.cs:261` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C29 | chat 401 unauthenticated; 404 `Feature disabled` | A: both passed | `tests/Api.Tests/Ai/ChatAiTests.cs:51` - `Assert.Equal(HttpStatusCode.Unauthorized, ...)`; `:34-36` - `Assert.Equal(HttpStatusCode.NotFound, ...)`, `Assert.Equal("Feature disabled", problem?.Title)` | PASS |
| C30 | comparisons 201/403/404/409/503 regressions | A: all 5 passed | `tests/Api.Tests/Ai/CompareModelsTests.cs:43` Created; `:303` Forbidden; `:115` NotFound; `:151` Conflict; `:172` ServiceUnavailable | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `history` roles (2 accepted, open rejected set) | code `ChatAi.cs:42` `AcceptedRoles {user, assistant}` OrdinalIgnoreCase + `role ?? ""` | user C5 · Assistant C5 · tool/system C1 (HTTP + validator) · "" / developer C1 (validator) | - |
| `history` item fields rejected (2) | plan door 7 + `ChatAi.cs` ToolCalls/ToolCallId rules | ToolCalls C2 · ToolCallId C2 | - |
| `history` bounds (4 edges) | plan AC 3-4, `ChatAi.cs` `MaxHistoryItems=50`, `MaxContentChars=4000` | 50/51 C3 · 4000/4001 C4 | - |
| tool errors seen by model, door 5 (4) | plan door 5; `ContentGuard.cs` constants | permission_denied C6 · tool_failed C7 · tool_not_found C9 · tool_output_blocked C14 | - |
| tool exceptions (3 catch arms) | code `AgentLoop.cs:82,86,91` | OCE-when-cancelled C8 · Unauthorized C6 · other C7 | - |
| guard subjects, door 3 (3) + default | plan door 3; call sites `AgentLoop.cs` (ToolOutput, Reply), `ChatAi.cs`/`CompareModels.cs` (UserMessage) | UserMessage C13, C17 · ToolOutput C14 · Reply C15 · default C16 | - |
| delimiter / suffix, door 4 (3) | plan door 4 + AC 10-11 | wrap C10 · case-insensitive escape C10 · suffix C11 | - |
| loop calls carrying the suffix (3) | code `AgentLoop.cs` (iteration request, fallback summary) | first, after tool, summary: C11 (6 requests asserted) | - |
| truncation (3 edges) | plan AC 12 | = limit, > limit, default 16000: C12 | - |
| rate limit, doors 1-2 (4) | plan doors 1-2, AC 18-19; `AiRateLimit.cs:57-62`; `HostApplicationExtensions.cs:33-37` | exceeded C18 · shared bucket C19 · tenant partition C20 · pipeline order C20 | - |
| daily quota, AC 20-21 (5) | plan AC 20-21 + Assumptions "soma desde 00:00 UTC"; `AiRateLimit.cs:77,80,81` | off (`<= 0`) C23 · at limit (`>=`) chat C21 · comparisons C21 · tenant/`since` filter C22 · **window start = 00:00 UTC of the day (`DateTime.UtcNow.Date`, `AiRateLimit.cs:80`)**: no proof | window start 00:00 UTC (AC 20) - no test pins `since`; a rolling-24h or lifetime window passes C21-C23 |
| `POST /api/v1/ai/chat` statuses (5) | plan `Surface` | 200 C5 · 400 C1, C13 · 401 C29 · 404 C29 · 429 C18, C21 | - |
| `POST /api/v1/ai/comparisons` statuses (8) | plan `Surface` | 201 C30 · 400 C17 · 401 C28 · 403 C30 · 404 C30 · 409 C30 · 429 C19, C21 · 503 C30 | - |
| routes with the `auth` policy (1 behaviour) | plan AC 23; `SecurityConfiguration.cs:137-140` | 429 with empty body C25 | - |
| new chat-screen error states (2) | binding screen `chat.ts:144` (the only changed branch) | 429 detail C26 · 400 `Message` C27 | - |
| startup config (2 assemblies) | read directly: `Program.cs:12` `AddFeatureModules()` -> `FeatureModulesConfiguration.cs:16` `AddAiModule()` -> `AiModule.cs:22` `AddPolicy<string, AiRateLimitPolicy>`, `AiModule.cs:32` `AddSingleton<IContentGuard, AllowAllContentGuard>`; `TestServiceFactory.cs:32` `services.AddAiModule()` | real Program: C13, C18 (HTTP) · TestServiceFactory: C12 default, C16 | - |

Level check: every claim that names a status code (C1, C5, C6, C13, C15, C17, C18, C19, C21, C24,
C25, C28, C29, C30) has a proof through `WebApplicationFactory<Program>`. AC 19, "tenant B keeps
getting 200", is proven only as a partition-key unit (C20) plus a source-order test. No HTTP run
uses two tenants. That is a non-blocking note, because C20 as written is met.

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decides, crosses the HTTP boundary (validator, message guard, quota, limiter) | `ChatAi.cs` (validator, guard call), `CompareModels.cs` (guard call), `AiRateLimit.cs` (`AiQuota`, `AiRateLimitPolicy`) | boundary + own layer; status and body at the boundary; one case per decision-table row at the layer | no - validator (C1 HTTP + C1-C4 layer), message guard (C13/C17 HTTP + C13 handler) and limiter (C18 HTTP + C20 layer) meet it. `AiQuota` fails: its window-start decision (`AiRateLimit.cs:80`) has no case at any level, and the `>=` throw row is proven only at the HTTP boundary (C21), not at the layer |
| Decides, no boundary (`AgentLoop` tool/error/delimiter/truncation/guard) | `AgentLoop.cs`, `ContentGuard.cs` (`AgentGuardrails`), `ToolRegistry.cs` | own layer; one case per Coverage member | yes - C6-C12 and C14 all in `AgentLoopGuardrailTests` against `AgentLoop` directly |
| Instrumentation (`AllowAllContentGuard`, DI registrations) | `AiModule.cs`, `ContentGuard.cs` | container resolution; resolved type | yes - C16 `Assert.IsType<AllowAllContentGuard>` (`AgentLoopGuardrailTests.cs:184`) |

Swept rows that cite existing constraints, re-read against the code:

- authorization, "routes keep `Authenticated` / `AiAgentsManage`": present at `ChatAi.cs:155` and `CompareModels.cs:199`.
- `Post_ShouldReturn403_WithoutAgentManage` exists at `CompareModelsTests.cs:291` and ran green in A.
- dependency failure, "`ChatAi_ShouldReturn500_WhenLlmHttpFails` existing": present at `ChatAiTests.cs:73`, asserting `InternalServerError` at `:90`.
- concurrency, "soft quota, documented": the doc comment is at `AiRateLimit.cs:70-73`.
- door 1 premise, "the key is not caller-controlled after auth": the JWT `tenant_id` must match the resolved tenant (`SecurityConfiguration.cs:112`, `"Invalid token: tenant mismatch."`).

All of these constraints are in the code.

## Faults injected

All faults ran in a scratch `git worktree` at a47ffa5. The real-tree porcelain was empty before
the run and empty after.

Front fault anomaly: running vitest from the worktree wrote an untracked cache, `.angular/cache/`,
into the real repo root. It appeared with symlinked node_modules and again with APFS-cloned
node_modules. No tracked file changed. I deleted the cache each time and re-confirmed the porcelain
matched the baseline before removing the worktree. The mutant's failure message, `expected
'Validation failed'`, shows the mutated worktree source was the one under test. The same test passes
against the real tree.

| Mutation | Location | Killed |
| --- | --- | --- |
| validator role set `{user, assistant}` -> `{user, assistant, tool}` | `src/Api/Features/Ai/ChatAi.cs:42` | yes - `Validator_ShouldFail_WhenHistoryRoleIsNotUserOrAssistant(role: "tool")` failed |
| removed `catch (UnauthorizedAccessException)` arm (falls to `tool_failed`) | `src/Api/Features/Ai/AgentLoop.cs:86-90` | yes - `RunAsync_ShouldReturnPermissionDenied_WhenToolThrowsUnauthorized` failed (strings differ) |
| closing-tag regex drops `RegexOptions.IgnoreCase` | `src/Api/Features/Ai/ContentGuard.cs:56` | yes - `RunAsync_ShouldNeutralizeClosingTag_InsideToolOutput` failed |
| partition key `tenantId.ToString("N")` -> constant `NoTenantKey` | `src/Api/Features/Ai/AiRateLimit.cs:61` | yes - `Policy_ShouldPartitionByTenant` failed |
| chat error drops `fieldError(problem, 'message') ??` | `src/web/src/app/features/ai/chat.ts:144` | yes - `400 mostra o erro de message` failed (`expected 'Validation failed'`) |

The 5-fault cap was reached. No fault was injected on the quota window start (`AiRateLimit.cs:80`).
It is recorded as unproven in Coverage from the search above rather than from a run.

Step 5, walking the flow with the user, did not run. A sub-agent cannot run it. It is still owed
for the `chat` screen's 429 and 400 states.

## Gate

- `dotnet test tests/Api.Tests` (42-name filter): 50 passed, 0 failed.
- `dotnet test tests/ArchitectureTests` (2 names): 2 passed, 0 failed.
- `dotnet test tests/E2ETests` (`OpenApiDocumentTests`): 1 passed, 0 failed.
- `ng test chat.spec.ts` (2 names): 2 passed, 0 failed.
- Total: 55 passed, 0 failed.

Verdict FAIL: one unproven Coverage member (the AC 20 window start) and one unmet Test policy row
(`AiQuota`).
