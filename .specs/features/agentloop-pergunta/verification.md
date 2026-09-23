# AgentLoop mantém a pergunta verification

**Verdict**: PASS
**Profile**: ui
**Diff range**: 752e128..9f8f018
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

None - the change touches no interface (no route, contract, screen or design); `checks.md` carries an `## Intent` and no plan, so there is nothing binding to compare. Step 5 (walk with user) is n/a: no UI.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| none (no interface change) | n/a | none | - |

## Checks

Proofs run once, batched: `dotnet test tests/Api.Tests --filter "<5 names joined by the OR operator>" --logger trx` at `9f8f018` - 5 passed, 0 failed; the trx lists each of the 5 names individually with outcome `Passed`. All four `AgentLoopTurnTests` are new in `9f8f018`; the `ChatAiHandlerTests` proof is pre-existing.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | 2nd call: History roles user,assistant,user,assistant,tool; contents u0,a0,q,"",tool_output; UserPrompt empty | `AgentLoopTurnTests.RunAsync_ShouldKeepUserMessage_BeforeToolCallTurn_OnLaterCalls` Passed | `tests/Api.Tests/Ai/AgentLoopTurnTests.cs:19` - `Assert.Equal(new[] { "user", "assistant", "user", "assistant", "tool" }, second.History!.Select(m => m.Role).ToArray())`; `:20` - `Assert.Equal(new[] { "u0", "a0", "q", "" }, second.History!.Take(4)...)`; `:21` - `Assert.StartsWith("<tool_output>", second.History![4].Content)`; `:22` - `Assert.Equal(string.Empty, second.UserPrompt)` | PASS |
| C2 | 1st call unchanged: UserPrompt "q", History = prior 2 items | `AgentLoopTurnTests.RunAsync_ShouldSendMessage_AsUserPrompt_OnFirstCall` Passed | `AgentLoopTurnTests.cs:33` - `Assert.Equal("q", first.UserPrompt)`; `:34` - `Assert.Equal(new[] { ("user", "u0"), ("assistant", "a0") }, first.History!.Select(m => (m.Role, m.Content)).ToArray())` | PASS |
| C3 | summary call after MaxIterations has exactly one user turn "q" | `AgentLoopTurnTests.RunAsync_ShouldKeepUserMessageOnce_InSummaryCall` Passed | `AgentLoopTurnTests.cs:49` - `Assert.Null(summary.Tools)` (it is the summary call); `:50` - `Assert.Single(summary.History!, m => m.Role == "user" && m.Content == "q")`; `:51` - `Assert.Single(summary.History!, m => m.Role == "user")` | PASS |
| C4 | TurnMessages = assistant,tool (no user); chat persists user,assistant,tool,assistant | `AgentLoopTurnTests.RunAsync_ShouldExcludeUserMessage_FromTurnMessages` Passed; `ChatAiHandlerTests.Handle_ShouldPersistToolTurns_InLoopOrder_WithSequenceIncrementingByOne` Passed | `AgentLoopTurnTests.cs:59` - `Assert.Equal(new[] { "assistant", "tool" }, result.TurnMessages!.Select(m => m.Role).ToArray())`; `tests/Api.Tests/Ai/ChatAiHandlerTests.cs:290` - `Assert.Equal(new[] { "user", "assistant", "tool", "assistant" }, items.Select(i => i.Role).ToArray())` | PASS |

## Coverage

Recomputed from `src/Api/Features/Ai/AgentLoop.cs` at `9f8f018`: `LlmRequest` is built at two sites - the loop body (`AgentLoop.cs:33`, reached as the first call and as every call after a tool) and the summary (`AgentLoop.cs:69`). The user insertion is guarded by `userMessage.Length > 0` (`:46`) and `userMessage` is cleared at `:63`, so the insertion happens once. `TurnMessages` consumers from `rg -n "TurnMessages" src`: only `ChatAi.cs:177` (`PersistTurnAsync`); `CompareModels.cs:141` calls `RunAsync` with `history: null` and does not read `TurnMessages`. Providers append `UserPrompt` after History only when not blank (`OpenRouterLlmService.cs:66`, `MicrosoftAgentFrameworkLlmService.cs:71`), which is why an empty `UserPrompt` plus the user turn in History is the right shape. The `Length > 0` vs `IsNullOrWhiteSpace` asymmetry is unreachable: both callers validate the message with `NotEmpty()` (`ChatAi.cs:23`, `CompareModels.cs:28`).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| loop LLM calls (3) | `AgentLoop.cs:33` (first + after tool), `AgentLoop.cs:69` (summary) | first C2 · after tool C1 · summary C3 (also asserts no duplicate user across 5 tool iterations) | - |
| TurnMessages consumers (1) | `rg TurnMessages src` -> `ChatAi.cs:177` | ChatAiHandler C4 | - |
| history shapes (2) | callers `ChatAi.cs:108` (prior history), `CompareModels.cs:141` (null) | prior history C1 C2 C4 · null history C3 | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, sem fronteira (`AgentLoop`) - one proof at own layer, one case per loop call | `src/Api/Features/Ai/AgentLoop.cs` | own layer: `AgentLoopTurnTests` C1 C2 C3 C4 (direct `AgentLoop` with `ScriptedLlmService`, same level as `AgentLoopGuardrailTests`) | yes - one case per each of the 3 calls |

Swept re-read: `failure modes: existing` cites `AgentLoop.ExecuteToolAsync` (present, `AgentLoop.cs:81`) proven in `AgentLoopGuardrailTests` - confirmed at `tests/Api.Tests/Ai/AgentLoopGuardrailTests.cs:30` (`permission_denied`) and `:44` (`tool_failed`). Other rows are n/a or point at C1/C3/C4.

## Faults injected

Scratch worktree at `scratchpad/verify-loop` (detached at `9f8f018`), removed after. Real-tree `git status --porcelain` empty before and after (identical). No `git stash`.

| Mutation | Location | Killed |
| --- | --- | --- |
| F1 drop the user insertion | `AgentLoop.cs:50` | yes - C1 fails; C3 fails when re-run under the same mutant (ChatAiHandler proof survives it, as expected: persistence is unchanged) |
| F2 insert the user turn after the assistant turn (turnStart adjusted to the user index) | `AgentLoop.cs:46-53` | yes - C1 fails |
| F3 do not move `turnStart` (user leaks into TurnMessages) | `AgentLoop.cs:51` | yes - C4 fails |
| F4 pass the mutable list instead of a copy on the loop request | `AgentLoop.cs:36` | yes - C2 fails (first request's History observed as 5 items) |

Every proof carrying a check (C1, C2, C3, C4 loop-level) was made to fail at least once. The summary site's `ToArray()` (`AgentLoop.cs:72`) was not mutated: nothing mutates the list after that call, so the mutant is equivalent.

## Gate

`dotnet test tests/Api.Tests` - 307 passed, 0 failed.
`make verify` - exit 0: ArchitectureTests 18 passed, Api.Tests 307 passed, E2ETests 13 passed, 0 failed.
