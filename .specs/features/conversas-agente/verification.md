# Conversas persistidas (threads/runs) verification

**Verdict**: PASS
**Profile**: ui
**Diff range**: 9b0db04..2d4df5a (round-4 range 6370024..2d4df5a)
**Round**: 4 - scoped (past the three-round bound, approved by the user)
**Verifier**: independent sub-agent (author != verifier)

Round 4 scope: commit `2d4df5a` (tests and STATE only; nothing in `src/`). (1) C5's HTTP proof now counts a fresh `AiHttp.PlainUserClientAsync` user instead of the shared admin. (2) `DevBootstrapSeederTests.CreateSeedProvider` gets `EnableServiceProviderCaching(false)` and its own `InMemoryDatabaseRoot` (AD-012). Verified at `2d4df5a`: C5, the determinism of the proof batch and full suite, the C5 fault, the model-cache hypothesis, and citations in `ChatAiTests.cs`, where everything after line 203 moved by +1 and is refreshed (C5, C6, C7, C8, C13, C14, C23). Everything else is `carried from 6370024` (round 3), which carried from `37de0a6` and `43eb477` where marked.

**Round-3 C5 finding - closed.** The C5 HTTP proof asserts `before == after` on a user created inside the test (`tests/Api.Tests/Ai/ChatAiTests.cs:206`, count at `:207` and `:220`). No other test can own that user's rows. Over 20 runs of the proof batch it was never red, against 2 of 5 red in round 3.

**Second flake - hypothesis reproduced.** The claim: EF caches the model per internal service provider, `DevBootstrapSeederTests` builds `AppDbContext` without `AddAiModule`, and if it builds the model first the Ai tenant query filters vanish for the whole run.
- **Test:** in a scratch worktree at `2d4df5a`, I reverted only `tests/Api.Tests/Host/DevBootstrapSeederTests.cs` to its `6370024` version and ran the full Api.Tests suite 20 times.
- **Result:** 3 of 20 runs failed (runs 7, 15, 17). Each failed exactly 9 tests, the same 9 every time, all Ai tenant-filter tests:
  - `ListAgentsTests.Handle_ShouldReturnTenantAgents_OrderedByCreatedAtDesc`
  - `ChatAiHandlerTests.Handle_ShouldThrow_WhenConversationBelongsToAnotherTenant` (a C6 proof)
  - `AiUsageTests.UsageEntries_ShouldBeTenantFiltered`
  - `CreateAgentTests.Seed_ShouldCreateDefaultAgent_ForDevTenant`
  - `CreateAgentTests.CreateTenant_ShouldSeedDefaultAgent`
  - `DeactivateAgentTests.Handle_ShouldThrow_WhenLastActiveAgent`
  - `AiRateLimitTests.UsageRepository_ShouldSumTokens_SinceInstant_ForCurrentTenant`
  - `CreateAgentFileTests.Handle_ShouldThrow_WhenAgentIsInAnotherTenant`
  - `CompareModelsTests.Get_ShouldReturn404_ForOtherTenantOrMissing`
- **With the fix:** the full suite at `2d4df5a` in the real tree ran 25 of 25 green.
- **Reading:** this matches the orchestrator's report (9 tests, about 1 run in 8) and supports the model-cache explanation. It is statistical evidence, not proof of the mechanism: 3/20 reverted against 0/25 fixed. If both runs had the same true failure rate of about 15%, 25 straight green runs would happen by chance about 1.7% of the time.

Real-tree porcelain was empty before the worktree was created and after it was removed, and afterwards showed only this file being rewritten. No `git stash`. No vitest ran in the worktree. No `.angular/cache` at the repo root.
Step 5 (walk with the user) cannot run from a sub-agent and did not run - the orchestrator owes it.

## Binding sources

Verified at `37de0a6` for the two screens; the other rows carried from `43eb477`.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| `agents-list.ts` + plan S3 paragraph -> `conversations-list` (verified at 37de0a6) | yes - re-read; `conversations-list.ts` unchanged by the fix | none; coverage: C46 now asserts the 4 regions in order (`src/web/src/app/features/ai/conversations-list.spec.ts:107`), header `h1` then `a` (`:110`), copy `Conversas` / `Nova conversa` -> `/ai` (`:111-113`), header `space-between` (`:114`), `Pesquisar` (`:116`), table inside `app-list-state` (`:117`), headers `Título`, `Atualizada`, actions (`:120`), sort only on `title`, `lastActivityAt` (`:122`), row `Apagar` (`:124`), title link (`:125`). Not asserted and not a design element: the search input's wiring to `store.load` (presence only) - noted, no binding source decides it | - |
| `chat.ts` + plan S3 paragraph -> `chat` (verified at 37de0a6) | yes - re-read; `chat.ts` unchanged by the fix | none; coverage: C47 asserts exactly two toolbar children, picker first and `Nova conversa` second (`src/web/src/app/features/ai/chat.spec.ts:459-461`), `flex` + `space-between` (`:462-463`), above the history (`:464`), inside the `mat-card` (`:465`). The round-1 note on `chat-notice` placement is withdrawn: no binding source places that band (AC 27 decides only its copy, covered by C27) | - |
| `src/web/src/app/shared/list-state.ts` (verified at 37de0a6) | yes | none; coverage: `Tentar de novo` now asserted (`src/web/src/app/shared/list-state.spec.ts:116`) | - |
| `src/web/src/app/shared/confirm.ts` (carried from 43eb477) | yes | none | - |
| `guardrails-agente/checks.md` `## Superseded` (verified at 37de0a6) | yes - fix diff re-read | none - the rename `ChatAi_ShouldReturnOnlyReplyAndIterations` -> `ChatAi_ShouldReturnConversationIdReplyAndIterations` is now listed; guardrails C26 proof renamed to `429 mostra o detail e repoe a mensagem no campo`, which exists and passed in the front batch (`chat.spec.ts:270-275`) | - |

## Checks

Proof runs at `2d4df5a`, one call per target:
- **Api.Tests proof batch:** 73 distinct names joined by `|`. Run 1 used a trx logger: 75 passed (two Theories), every name found individually as Passed, none missing. The same batch was run 20 times in total: 20 passed, 0 failed.
- **Api.Tests full suite:** 25 runs, 25 passed (303 tests each), 0 failed.
- **Front:** `ng test --no-watch` with 5 `--include` files and one `--filter` alternation of the 17 check names (run through `npx -y node@24.15.0`, environment workaround). Exit 0; 17 passed, each listed individually.
- **ArchitectureTests:** 18 passed.
- **E2ETests:** 13 passed.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | no conversationId creates tenant+user conversation, 200 with conversationId/reply/iterationsUsed | batch, passed (citations refreshed) | `tests/Api.Tests/Ai/ChatAiTests.cs:180` - `Assert.NotEqual(Guid.Empty, body!.ConversationId)`; `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:75-76` - tenant and user of the created conversation | PASS |
| C2 | one SaveChangesAsync per successful turn | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:95` - `Assert.Equal(1, counter.Calls)` | PASS |
| C3 | append after highest sequence, 200 same id | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:110` - sequences `1..5`; `tests/Api.Tests/Ai/ChatAiTests.cs:197` - same `ConversationId` | PASS |
| C4 | window of last HistoryWindow user/assistant items, oldest first, body ignored | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:126-128`; `:146` - `new[] { "a1", "u2", "a2" }` | PASS |
| C5 | history -> 400 Validation failed, key history, no LLM, no persist (verified at 2d4df5a) | batch, both names passed in 20 of 20 runs | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:61-63` - `ShouldHaveValidationErrorFor("history")`; `tests/Api.Tests/Ai/ChatAiTests.cs:215-219` - 400, `Validation failed`, key `history`, `Assert.Empty(llm.Requests)`; `:220` - `Assert.Equal(before, await ConversationCountAsync(client))` for a fresh plain user (`:206`), so no parallel test can move the count. Faults R4-1 and R4-2 killed | PASS |
| C6 | unknown or foreign conversationId -> 404 Not found, same body | batch, 4 names passed | boundary: `tests/Api.Tests/Ai/ChatAiTests.cs:285-290` - both `NotFound`, `Assert.Equal(unknownBody, foreignBody)` after id substitution, title `Not found`; another tenant, same user id: `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:540-543` - `NotFoundException`, same message, no LLM call; own level `:172`, `:185` | PASS |
| C7 | agentId mismatch -> 409, no items appended | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:201-203`; `tests/Api.Tests/Ai/ChatAiTests.cs:239-241` | PASS |
| C8 | fixed agent inactive -> 404 Not found, loop not run | batch, both passed | boundary: `tests/Api.Tests/Ai/ChatAiTests.cs:307-309` - `NotFound`, `"Not found"`, `Assert.Equal(before, llm.Requests.Count)`; own level `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:223` | PASS |
| C9 | loop throws -> counts unchanged, nothing created, 500 Unexpected error | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:235`, `:245-246`; `tests/Api.Tests/Ai/ChatAiTests.cs:90-92` | PASS |
| C10 | tool turns in loop order, sequence +1 | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:290-291` | PASS |
| C11 | lastActivityAt = append instant | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:322` | PASS |
| C12 | title first 80 chars + ellipsis | batch, 2 rows passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:336` | PASS |
| C13 | MaxItems -> 409, no LLM | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:352-353`; `tests/Api.Tests/Ai/ChatAiTests.cs:256-259` | PASS |
| C14 | concurrent same sequence -> second write 409, no overwrite | batch, 3 names passed | boundary: `tests/Api.Tests/Ai/ChatAiTests.cs:336-342` - statuses `{ OK, Conflict }`, title `Business rule violation`, detail `ConcurrentAppendMessage`; no overwrite: `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:386-388` - seed rows 1,2 keep content, winner at 3,4; model: `:555` `Assert.True(index.IsUnique)` on `(ConversationId, Sequence)` and `:556-557` `LastActivityAt` is a concurrency token. Judged as written: the claim is proven. Declared limit (plan Impact): rollback of the loser's rows and DB enforcement of the unique index are Postgres-only and not exercised by InMemory | PASS |
| C15 | EnableAI false -> 404 Feature disabled on 4 routes | batch, 4 passed | `tests/Api.Tests/Ai/ChatAiTests.cs:34-36`; `tests/Api.Tests/Ai/ListConversationsTests.cs:90-91`; `tests/Api.Tests/Ai/GetConversationTests.cs:119-120`; `tests/Api.Tests/Ai/DeleteConversationTests.cs:101-102` | PASS |
| C16 | every chat exit logs tenantId, agentId, conversationId (verified at 6370024) | batch, 5 names passed | code: one outer `try`/`finally` around `RunTurnAsync` writes the line on every exit after tenant and user resolve (`src/Api/Features/Ai/ChatAi.cs:62-78`); success `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:403-406`; loop throws `:421-424`; MaxItems 409, agent mismatch 409, unknown 404 `:448-451`; quota 429 `:472-475`; guard block 400 `:495-497` - tenant, `seeded.AgentId`, `seeded.Id` in the single finished line; inactive agent 404 `:516-519` - tenant, agent, conversation, `success False`. Faults R2-1 and R3-1 killed | PASS |
| C17 | GET list: defaults, only caller's, lastActivityAt desc | batch, 3 names passed | boundary: `tests/Api.Tests/Ai/ListConversationsTests.cs:45-47` - two distinct plain users, `TotalCount` 2, `new[] { alicesSecond, alicesFirst }`, Bob's absent; defaults `:25-28`; search and sorts `:63-66` | PASS |
| C18 | GET one: user+assistant by sequence (carried from 43eb477, citations refreshed) | batch, passed | `tests/Api.Tests/Ai/GetConversationTests.cs:27`; boundary `:134` | PASS |
| C19 | includeToolItems=true includes tool items as stored | batch, both passed | boundary: `tests/Api.Tests/Ai/GetConversationTests.cs:94-97` - no tool without the parameter, one tool with `?includeToolItems=true`, content starts `<tool_output>\n`, sequences `1..4`; own level `:38-39`. Fault R2-3 killed only by the HTTP proof | PASS |
| C20 | another user's conversation -> GET and DELETE 404 Not found, never 403 | batch, 4 names passed | boundary: `tests/Api.Tests/Ai/GetConversationTests.cs:73-76` - stranger and Admin `NotFound`, title `Not found`, owner `OK`; `tests/Api.Tests/Ai/DeleteConversationTests.cs:76-79` - same, row still readable by owner; own level `GetConversationTests.cs:48`, `DeleteConversationTests.cs:33-35` | PASS |
| C21 | DELETE removes row+items, 204, then GET 404 (carried from 43eb477) | batch, passed | `tests/Api.Tests/Ai/DeleteConversationTests.cs:22-24`, `:59-61` | PASS |
| C22 | owner filter with no Admin exception | batch, 4 names passed | boundary: `tests/Api.Tests/Ai/GetConversationTests.cs:74`, `tests/Api.Tests/Ai/DeleteConversationTests.cs:77` - Admin gets `NotFound`; chat write as Admin on another user's conversation `tests/Api.Tests/Ai/ChatAiTests.cs:285`; own level `GetConversationTests.cs:57`, `DeleteConversationTests.cs:44-46` | PASS |
| C23 | Authenticated, no ai.agent.read/manage | batch, 4 passed | `tests/Api.Tests/Ai/ChatAiTests.cs:270`; `tests/Api.Tests/Ai/ListConversationsTests.cs:103-105`; `tests/Api.Tests/Ai/GetConversationTests.cs:132`; `tests/Api.Tests/Ai/DeleteConversationTests.cs:114` | PASS |
| C24 | unauthenticated -> 401 on 4 routes | batch, 4 passed | `tests/Api.Tests/Ai/ChatAiTests.cs:51`; `tests/Api.Tests/Ai/ListConversationsTests.cs:116`; `tests/Api.Tests/Ai/GetConversationTests.cs:145`; `tests/Api.Tests/Ai/DeleteConversationTests.cs:125` | PASS |
| C25 | /ai initial state (carried from 43eb477) | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:306-309` | PASS |
| C26 | load via GET, chat-loading, only user+assistant (carried) | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:333`, `:338-341` | PASS |
| C27 | 404 -> `Conversa não encontrada`, new conversation (carried) | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:361-362`, `:367` | PASS |
| C28 | first POST stores id, navigates without reload (carried) | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:383-385` | PASS |
| C29 | picker disabled with items; Nova conversa reactivates (carried) | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:399-400`, `:405-407` | PASS |
| C30 | POST failure removes optimistic turn, restores draft (carried) | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:422-424` | PASS |
| C31 | empty list copy and action (carried) | front batch, passed | `src/web/src/app/features/ai/conversations-list.spec.ts:46-49` | PASS |
| C32 | loading bar, paginator disabled | front batch, passed | `src/web/src/app/shared/list-state.spec.ts:75-78` | PASS |
| C33 | 500 shows title and `Tentar de novo` | front batch, passed | `src/web/src/app/shared/list-state.spec.ts:115` - `toBe('Unexpected error')`; `:116` - `expect(text(fixture, 'list-retry')).toBe('Tentar de novo')` | PASS |
| C34 | confirm copy -> DELETE and row removed; cancel emits nothing (carried) | front batch, both passed | `src/web/src/app/features/ai/conversations-list.spec.ts:70-79`, `:94-95` | PASS |
| C35 | flag off hides three nav links (carried) | front batch, passed | `src/web/src/app/shell/shell.spec.ts:85-87` | PASS |
| C36 | flat files, no layer folders, no history sent (carried) | front batch, 3 passed | `src/web/src/app/features/ai/chat.spec.ts:446-447`; `architecture.spec.ts` names passed | PASS |
| C37 | purge older than cutoff with items (carried) | batch, passed | `tests/Api.Tests/Ai/ConversationRetentionServiceTests.cs:25-29` | PASS |
| C38 | RetentionDays 0 deletes nothing (carried) | batch, passed | `tests/Api.Tests/Ai/ConversationRetentionServiceTests.cs:41-42` | PASS |
| C39 | ignores query filters, logs count (carried) | batch, passed | `tests/Api.Tests/Ai/ConversationRetentionServiceTests.cs:63-65` | PASS |
| C40 | failed pass logged, scheduling continues at 24h (carried) | batch, passed | `tests/Api.Tests/Ai/ConversationRetentionServiceTests.cs:85-87` | PASS |
| C41 | appsettings block 20/200/90/24 (carried) | batch, passed | `tests/Api.Tests/Ai/ConversationRetentionServiceTests.cs:97-107`; `src/Api/appsettings.json:56-61` | PASS |
| C42 | guard or quota refusal persists nothing | batch, both passed (citations refreshed) | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:259-260`, `:276-277`; boundary statuses `tests/Api.Tests/Ai/ChatAiTests.cs:144`, `tests/Api.Tests/Ai/AiRateLimitTests.cs:117` | PASS |
| C43 | stored tool content = delivered to LLM | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:305-307` | PASS |
| C44 | no empty assistant in the window | batch, passed | `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:160-161` | PASS |
| C45 | chat 429 ProblemDetails (carried) | batch, both passed | `tests/Api.Tests/Ai/AiRateLimitTests.cs:38-41`, `:117-119` | PASS |
| C46 | conversations-list regions, copy, columns, sort headers, row action, link | front batch, passed | `src/web/src/app/features/ai/conversations-list.spec.ts:107` - `toEqual(['header', 'mat-form-field', 'app-list-state', 'mat-paginator'])`; `:110-125` (see Binding sources). Fault R2-5 killed | PASS |
| C47 | chat toolbar: picker left, Nova conversa right, flex space-between, above history, in mat-card | front batch, passed | `src/web/src/app/features/ai/chat.spec.ts:459-465` | PASS |
| C48 | 400 for pageSize 0 or 101 and for the empty Guid on GET and DELETE | batch, 3 names (4 runs) passed | `tests/Api.Tests/Ai/ListConversationsTests.cs:79` - `BadRequest` for both Theory rows; `tests/Api.Tests/Ai/GetConversationTests.cs:108`; `tests/Api.Tests/Ai/DeleteConversationTests.cs:90`; contract declares 400 on the three routes (`openapi.json`, pinned by E2E). Fault R2-4 killed | PASS |

## Coverage

Rows the fix touched are recomputed at `37de0a6`; the rest are carried from `43eb477`.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/v1/ai/chat` statuses (7) - carried | Surface + `ChatAi.cs` `Produces*` + openapi | 200 C1,C3 · 400 C5 · 401 C24 · 404 C6,C8,C15 · 409 C7,C13,C14 · 429 C45 · 500 C9 - all at HTTP | - |
| `GET /api/v1/ai/conversations` statuses (4) - verified at 37de0a6 | Surface (now with 400) + `ListConversations.cs` + `Shared/Pagination.cs:24-25` + openapi | 200 C17,C23 · 400 C48 (pageSize 0, 101) · 401 C24 · 404 C15 | - |
| `GET /{conversationId}` statuses (4) - verified at 37de0a6 | Surface + `GetConversation.cs` (`NotEmpty`) + openapi | 200 C18,C19,C23 · 400 C48 · 401 C24 · 404 C15,C20,C21 | - |
| `DELETE /{conversationId}` statuses (4) - verified at 37de0a6 | Surface + `DeleteConversation.cs` + openapi | 204 C21,C23 · 400 C48 · 401 C24 · 404 C15,C20 | - |
| list search and sort (5) - verified at 37de0a6 | Surface In + `Conversation.cs:104-127` | `searchTerm` `ListConversationsTests.cs:63` · `title` asc `:64` · `title` desc `:65` · `lastActivityAt` asc `:66` · default desc `:28` and over HTTP `:46` - search and sort asserted at handler level; their query-string binding shares `[AsParameters]` with the HTTP-proven defaults | - |
| `GET /{id}` inputs (2) - verified at 37de0a6 | Surface | `conversationId` C18 · `includeToolItems` C19 at HTTP | - |
| conversation not found for the caller (4) - verified at 37de0a6 | AC 6 + door 3 + tenant filter `AiModule.cs:118-119` | random id C6 (HTTP) · another user C6 (HTTP), C20 · another tenant, same user id C6 `ChatAiHandlerTests.cs:496-499` (fault R2-2 killed) · Admin non-owner C22 | - |
| chat exits that write the completion line (8) - verified at 6370024 | AC 16 + `ChatAi.cs:62-78` + checks.md Coverage row | success `ChatAiHandlerTests.cs:403-406` · loop throws `:421-424` · unknown conversation 404, agent mismatch 409, MaxItems 409 `:448-451` · quota 429 `:472-475` · guard block 400 `:495-497` · inactive agent 404 `:516-519` - all 8 asserted individually. Tenant or user missing (`ChatAi.cs:54-57`) throws before the line and is unreachable over HTTP with the `Authenticated` policy and a resolved tenant | - |
| refusals before the turn that persist nothing (3) - carried | plan Assumptions | loop throws C9 · guard C42 · quota C42 | - |
| one-way doors (7) - door 3 and 4 verified at 37de0a6 | Landing | 1 C1,C2 · 2 C5 · 3 C6,C17,C20,C22 (now at HTTP) · 4 C10,C14 (HTTP 409, unique index and concurrency token asserted on the model) · 5 C21,C37 · 6 C37-C41 · 7 C7 · declared in plan Impact and not exercised on InMemory: DB enforcement of the unique index and the loser's rollback (Postgres-only) | - |
| Relations entities (2) - carried | Relations | Conversation C1 · ConversationItem C2,C10 | - |
| response shapes (4) - carried | Surface Out vs openapi | pinned by `OpenApiDocumentTests` (ran, passed) | - |
| screens (2) - verified at 37de0a6 | binding sources | chat C25-C30, C47 · conversations-list C31-C34, C46 | - |
| startup config (1 assembly) - carried | `src/Api/appsettings.json:56-61` | C41 | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, reached across a boundary (verified at 37de0a6) | `IConversationRepository` ownership | boundary + own level | yes - boundary: chat `ChatAiTests.cs:285-290`, get `GetConversationTests.cs:73-76`, delete `DeleteConversationTests.cs:76-79`, list `ListConversationsTests.cs:45-47`; own level one case per route (C6, C17, C20, C22) plus the other-tenant case |
| Decide, reached across a boundary (verified at 37de0a6) | sequence conflict (`ChatAi.cs`, `PersistTurnAsync` catch) | boundary + own level | yes - boundary `ChatAiTests.cs:336-342`; own level `ChatAiHandlerTests.cs:377-388` |
| Decide, reached across a boundary (carried from 43eb477) | `conversations-list.ts`, `chat.ts` | MSW boundary + own level | yes - now including arrangement (C46, C47) |
| Decide, not reached across a boundary (carried) | history window | one case per decision row | yes |
| Decide, not reached across a boundary (carried) | `ConversationRetentionService` | one case per decision row | yes |
| Entry point that decides nothing (carried) | none classified | - | n/a |
| Instrumentation, pass-through (carried) | none classified | - | n/a |

Swept rows resolving to existing constraints: carried from `43eb477` (all present; the fix did not
touch `ChatAi.cs:23`, `chat.spec.ts:196`, `chat.spec.ts:248-250`, `AiRateLimitTests.cs`).

## Faults injected

Round 4, on the C5 surface, in a scratch worktree at `2d4df5a`. Earlier faults are carried from `6370024`; `src/` is unchanged since round 3.

| Mutation | Location | Killed |
| --- | --- | --- |
| R4-1: validator accepts a `history` made only of `user` turns (`.Null()` -> `.Must(h => h is null or all roles are user)`) | `src/Api/Features/Ai/ChatAi.cs:26` | yes - `ChatAiHandlerTests.Validator_ShouldFail_WhenHistoryIsProvided` failed. The HTTP proof passed, which is correct: it sends a `tool` turn, and the mutant still rejects that |
| R4-2: validator accepts any `history` (`.Null()` -> `.Must(_ => true)`) | `src/Api/Features/Ai/ChatAi.cs:26` | yes - `ChatAiTests.ChatAi_ShouldReturn400_WhenHistoryIsProvided_AndNotCallLlmOrPersist` failed (`Expected: BadRequest Actual: OK`) |
| R3-1 (carried from 6370024) | see round 3 | yes |
| R2-1 to R2-5 (carried from 37de0a6) | see round 2 | yes |
| round-1 five faults (carried from 43eb477) | see round 1 | yes |

The hypothesis run (seeder fix reverted, 20 full-suite runs, 3 red) is reported above. It is an experiment about the test harness, not a fault in feature code.

## Gate

At `2d4df5a`:
- `dotnet test tests/Api.Tests`, full suite: 303 passed, 0 failed, in 25 of 25 runs.
- The filtered Api.Tests proof batch: 75 passed, 0 failed, in 20 of 20 runs.
- `dotnet test tests/ArchitectureTests`: 18 passed, 0 failed.
- `dotnet test tests/E2ETests`: 13 passed, 0 failed.
- `ng test --no-watch` (src/web, all specs): 145 passed, 0 failed.

Total per run: 479 passed, 0 failed.
