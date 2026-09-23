# Workflows de agentes verification

**Verdict**: PASS
**Profile**: ui
**Diff range**: 6c038f0..fd704c7
**Round**: 3 - scoped (fix diff 4c038cb..fd704c7)
**Verifier**: independent sub-agent (author != verifier)

Round 3 re-judged every item that was not PASS at `4c038cb`, the two new checks C87 and C88, and
every file the fix diff touches. `git diff --stat 4c038cb..fd704c7` shows two files:
`tests/Api.Tests/Ai/WorkflowRunnerTests.cs` (+15) and `.specs/features/workflows-agente/checks.md`
(+9/-1: the count line, C87, C88 and a handoff line). No production file changed, so
`WorkflowRunner.cs` is the code round 2 read. Every proof was re-run in full at `fd704c7`. Anything
else is marked `carried from 4c038cb`.

Round 2 failed on four items. All four are now closed:

1. **Timeout and QuotaExceeded usage rows are asserted.** The tests behind C36 and C38 now read the
   ledger (`WorkflowRunnerTests.cs:233-236`, `:277-280`).
2. **Fault e is killed.** The round-2 survivor was an early return before `TrackAsync` for
   QuotaExceeded. Re-injected, it now fails C38's test. A new fault f, which skips `TrackAsync` on
   Timeout, fails C36's test.
3. **The `WorkflowRunner.cs` Test policy row is met.** Its usage-entry table has all 5 rows.
4. **The editor's layout and labels have a check.** C87 claims them, and its `--filter` selects the
   test. C88 does the same for the extra bounds test.

## Binding sources

Verified at `fd704c7` for `workflow-editor`, the only screen whose checks the fix touched (C87).
The other rows are carried from 4c038cb.

The plan declares no binding design (`plan.md:8`). The per-screen enumeration therefore measures
the checks against the plan's AC and its `Observable` rows (`plan.md:183-200`).

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| design mockup | none declared - `plan.md:8` (carried from 4c038cb) | - | - |
| screen `workflows-list` (AC 38-44, `plan.md:183-188`) | yes - carried from 4c038cb | none | - |
| screen `workflow-editor` (AC 45-57, `plan.md:189-194`) | yes - `workflow-editor.spec.ts` and `checks.md:294-295` at fd704c7 | none | - |
| screen `workflow-run` (AC 58-66, `plan.md:195-200`) | yes - carried from 4c038cb | none | - |

**How C87 closes the round-2 gap on `workflow-editor`.** C87 claims:

- the three regions, in the order palette, canvas, panel on the right, which matches `plan.md:193` ("painel lateral à direita");
- the five button labels.

The C87 filter `arranjo: paleta, canvas e painel a direita, com os rotulos` selects the test at
`workflow-editor.spec.ts:293`. That test ran and passed.

Precision gaps in the checks, recorded in prose only. The claim text was not widened, but each
behaviour is proven inside that check's own proof filter:

- **Carried from 4c038cb:** C60, C63, C69, C75, C76 and C79.
- **New this round:** the C36 and C38 claims still do not name the ledger row, though their tests
  now assert it. C41's claim ("cada passo terminado") is the one that covers those rows.

## Checks

All proof runs are at `fd704c7`:

- **Backend.** One invocation: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~CreateWorkflowTests|FullyQualifiedName~RunWorkflowTests|FullyQualifiedName~WorkflowRunnerTests" --logger "console;verbosity=normal"`. Exit 0, 93/93 passed. The named tests appear individually as `Aprovado`, including:
  - `RunOnce_ShouldFailStepWithTimeout_WhenStepExceedsTimeout` [1 s]
  - `RunOnce_ShouldFailStepWithQuotaExceeded_WhenQuotaExhausted` [685 ms]
  - `RunOnce_ShouldTrackUsage_PerStep_WithWorkflowOperation`
  - `Post_ShouldReturn400_ForBoundsBeyondPlan`, all three cases: `description 1001`, `key vazia`, `key 51`
- **Contract.** `dotnet test tests/ArchitectureTests --filter FullyQualifiedName~OpenApiContractTests`: exit 0, 5/5.
- **Migrations.** `dotnet ef migrations has-pending-model-changes --project src/Api`: exit 0, "No changes have been made to the model since the last migration."
- **Front dependencies.** `git diff --exit-code 6c038f0 -- src/web/package.json`: exit 0, empty diff.
- **Front.** One invocation from `src/web`: `npx -y node@24.15.0 node_modules/@angular/cli/bin/ng.js test --no-watch --include src/app/features/ai/workflows-list.spec.ts --include src/app/features/ai/workflow-editor.spec.ts --include src/app/shell/shell.spec.ts --include src/app/architecture.spec.ts --reporters=verbose`. Exit 0, 4 files, 60/60 passed. This includes `WorkflowEditor > arranjo: paleta, canvas e painel a direita, com os rotulos` (77 ms).

How the table is marked:

- **verified at fd704c7** - C36, C38 and C41, whose test file the fix touched; C87 and C88, which are new; and the `WorkflowRunnerTests.cs` rows below line 237, whose citations were refreshed because the lines moved by +4 or +8.
- **carried from 4c038cb** - every other row. Its evidence is unchanged, and its proof was re-run green above.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | POST 201 + Location + body | Api.Tests batch, exit 0 - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:37` - `Assert.Equal(HttpStatusCode.Created, response.StatusCode)`; `:39` Location | PASS |
| C2 | 12 invalid bodies -> 400 on key | Api.Tests batch, 12 cases - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:80` - `response.StatusCode == HttpStatusCode.BadRequest`; `:82` - `problem.Errors.ContainsKey(key)` | PASS |
| C3 | diamond accepted | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:118` - `Assert.Equal(HttpStatusCode.Created, response.StatusCode)` | PASS |
| C4 | FindCycle at own level | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:124-129` - `Assert.NotNull(WorkflowGraph.FindCycle(["a"], [("a", "a")]))` ... `Assert.Null(...)` | PASS |
| C5 | unknown/inactive agent -> 400 nodes | Api.Tests batch, 2 cases - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:148` - `BadRequest`; `:150` - `problem.Errors.ContainsKey("nodes")` | PASS |
| C6 | PUT replaces graph, updatedAt advances | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:174` - `Assert.Equal(["a", "c"], updated.Nodes.Select(n => n.Key))`; `:176` | PASS |
| C7 | PUT cycle -> 400 edges, graph kept | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:196` - `problem.Errors.ContainsKey("edges")`; `:198` - `Assert.Equal(workflow.Edges, stored.Edges)` | PASS |
| C8 | list active only, updatedAt desc | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:219` - `Assert.Equal([first.WorkflowId, second.WorkflowId], ...)` | PASS |
| C9 | fractional x/y as saved | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:239-240` - `Assert.Equal(12.5, node.X)`; `Assert.Equal(-40.25, node.Y)` | PASS |
| C10 | DELETE 204, isActive=false, off list | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:252` - `NoContent`; `:254` - `Assert.False(stored.IsActive)`; `:256` | PASS |
| C11 | unknown id -> 404 on 4 routes | Api.Tests batch, 4 cases - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:281` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)` | PASS |
| C12 | other tenant -> null in repo | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:295` - `Assert.Null(await repository.GetByIdAsync(workflow.WorkflowId))` | PASS |
| C13 | no perms -> 403 on 8 routes | Api.Tests batch, 8 cases - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:319` - `Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode)` | PASS |
| C14 | policy per route from metadata | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:348-355` - `Assert.Equal(SecurityPolicies.AiAgentsManage, PolicyOf("POST", Base))` ... | PASS |
| C15 | flag off -> 404 on 8 routes | Api.Tests batch, 8 cases - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:377` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)` | PASS |
| C16 | unique indexes + Restrict FKs | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:394` - `HasUniqueIndex(node, "WorkflowId", "Key")`; `:396` - `DeleteBehavior.Restrict` | PASS |
| C17 | migration covers model | `dotnet ef migrations has-pending-model-changes` exit 0 at fd704c7 - carried from 4c038cb | `src/Api/Shared/Migrations/20260923134725_AddWorkflows.cs:16` - `name: "AiWorkflows"`; `:114` | PASS |
| C18 | 202 + Location + Queued/Pending | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:38` - `Accepted`; `:41` - `Assert.Equal("Queued", run.Status)`; `:43` | PASS |
| C19 | empty / 4001 input -> 400 input | Api.Tests batch, 2 cases - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:64` - `BadRequest`; `:66` - `problem.Errors.ContainsKey("input")` | PASS |
| C20 | guard blocks -> 400, nothing stored | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:78` - `BadRequest`; `:80` - `Assert.Empty(page.Data)` | PASS |
| C21 | quota exhausted -> 429, nothing stored | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:96` - `TooManyRequests`; `:98` - `Assert.Empty(page.Data)` | PASS |
| C22 | rate limit -> second 429 | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:111-112` - `Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode)` | PASS |
| C23 | runs route declares 429 in openapi | ArchitectureTests exit 0 at fd704c7 - carried from 4c038cb | `tests/ArchitectureTests/OpenApiContractTests.cs:153` - `Assert.True(undeclared.Count == 0, ...)` | PASS |
| C24 | principal snapshot | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:126` - `principal.UserId`; `:128` - `Assert.Contains("Admin", principal.Roles)` | PASS |
| C25 | graph copied, later PUT no effect | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:149-150` - `Assert.Equal(["a", "b"], run.Nodes.Select(n => n.Key))` | PASS |
| C26 | deactivated workflow run -> 404 | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:163` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)` | PASS |
| C27 | hosted worker reaches Succeeded | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:182` - `Assert.Equal("Succeeded", run.Status)` | PASS |
| C28 | claim Running + startedAt before LLM | Api.Tests batch - carried from 4c038cb (lines unmoved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:38` - `Assert.Equal(WorkflowRunStatus.Running, seen!.Status)`; `:39` | PASS |
| C29 | D after B and C | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:67-68` - `Assert.True(starts["d"] >= ends["b"])` | PASS |
| C30 | max concurrency exactly 3 | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:95` - `Assert.Equal(3, max)` | PASS |
| C31 | default 3 from appsettings | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:104` - `Assert.Equal(3, options.MaxParallelSteps)` | PASS |
| C32 | exact message text, 3 cases | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:135-137` - `"Extraia\n\nhello"`, `"hello"`, `"Junte\n\nhello\n\n--- a ---\nsaida-a\n\n--- b ---\nsaida-b"` | PASS |
| C33 | A persisted before B called | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:160-168` - `Assert.Equal(WorkflowStepStatus.Succeeded, aWhenBStarted!.Status)` | PASS |
| C34 | all Succeeded -> run Succeeded + finishedAt | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:182-183` - `Assert.Equal(WorkflowRunStatus.Succeeded, run.Status)` | PASS |
| C35 | fail A, skip B, run C, run Failed | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:205-209` - `Failed` / `nameof(HttpRequestException)` / `Skipped` / `Succeeded` | PASS |
| C36 | Timeout errorCode, run Failed | Api.Tests batch, `RunOnce_ShouldFailStepWithTimeout_WhenStepExceedsTimeout` Aprovado - verified at fd704c7 | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:231` - `Assert.Equal("Timeout", run.Step("a").ErrorCode)`; `:232` - `Assert.Equal(WorkflowRunStatus.Failed, run.Status)`. New ledger assertions: `:233` - `var entry = Assert.Single(await WorkflowUsageAsync(factory))`; `:234` - `Assert.False(entry.Success)`; `:235` - `Assert.Equal("Timeout", entry.ErrorCode)`; `:236` - `entry.LatencyMs >= 900` (fault f killed) | PASS |
| C37 | AgentUnavailable, 0 LLM calls | Api.Tests batch - verified at fd704c7 (citation moved +4) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:254` - `Assert.Equal("AgentUnavailable", run.Step("a").ErrorCode)`; `:255` - `Assert.Empty(llm.Requests)` | PASS |
| C38 | QuotaExceeded, 0 LLM calls | Api.Tests batch, `RunOnce_ShouldFailStepWithQuotaExceeded_WhenQuotaExhausted` Aprovado - verified at fd704c7 | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:275` - `Assert.Equal("QuotaExceeded", run.Step("a").ErrorCode)`; `:276` - `Assert.Equal(callsBefore, llm.Requests.Count)`. New ledger assertions: `:277` - `Assert.Single(await WorkflowUsageAsync(factory))`; `:278` - `Assert.False(entry.Success)`; `:279` - `Assert.Equal("QuotaExceeded", entry.ErrorCode)`; `:280` - `Assert.Equal(0, entry.InputTokens + entry.OutputTokens)` (fault e killed). The helper filters `Operation == AiUsageOperations.Workflow` (`:66`), so the precondition's chat call is excluded. | PASS |
| C39 | tools run as launcher, 2 cases | Api.Tests batch, 2 cases - verified at fd704c7 (citation moved +8) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:309` - `Assert.Equal(denied, output.Contains("permission_denied"))` | PASS |
| C40 | step in run tenant | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:334` - `Assert.Contains($"tenant={TenantId}", Assert.Single(toolOutputs))` | PASS |
| C41 | every finished step writes a `workflow` usage entry; failed -> success=false + errorCode | Api.Tests batch - verified at fd704c7 | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:361` - `Assert.Equal(3, entries.Count)`; `:363` - `Assert.True(ok.Success)`; `:370` - `Assert.False(failed.Success)`; `:374` - `Assert.Equal("AgentUnavailable", refused.ErrorCode)`. All 5 endings are now proven: Timeout via `:233-236` and QuotaExceeded via `:277-280`. Every branch falls through to `TrackAsync` at `src/Api/Features/Ai/WorkflowRunner.cs:290`. | PASS |
| C42 | only one claim wins | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:395-396` - `Assert.True(await firstRepo.TryClaimAsync(...))`; `Assert.False(await secondRepo.TryClaimAsync(...))` | PASS |
| C43 | two parallel passes -> 2 LLM calls | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:412` - `Assert.Equal(2, llm.Requests.Count)` | PASS |
| C44 | interrupt >30 min, 2 cases | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:439` - `Assert.Equal("Interrupted", interrupted.ErrorCode)` | PASS |
| C45 | worker runs copied graph | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:471` - `Assert.Contains("role:antigo", prompt)` | PASS |
| C46 | LogError with runId, next run Succeeded | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:494` - `e.Level == LogLevel.Error && e.Message.Contains(broken.RunId.ToString())`; `:499` - `Assert.Equal("InternalError", aborted.ErrorCode)` | PASS |
| C47 | loop waits interval, survives a failed pass | Api.Tests batch - verified at fd704c7 (citation moved) | `tests/Api.Tests/Ai/WorkflowRunnerTests.cs:517` - `scopes.Calls >= 3`; `:519` - `Assert.Equal(TimeSpan.FromSeconds(7), clock.LastDueTime)` | PASS |
| C48 | full run shape | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:199-223` - `RunId`, `"Succeeded"`, `TotalCost 0.002m`, step fields | PASS |
| C49 | totalCost sum or null | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:237` - `Assert.Equal(0.003m, priced.TotalCost)`; `:241` - `Assert.Null(unpriced.TotalCost)` | PASS |
| C50 | runs list preview 200, newest first | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:257` - `Assert.Equal([newer.RunId, older.RunId], ...)`; `:259` | PASS |
| C51 | run of other workflow -> 404 | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:280` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)` | PASS |
| C52 | run of other tenant -> null | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:294` - `Assert.Null(await ...GetByIdAsync(run.RunId))` | PASS |
| C53 | exact status sets | Api.Tests batch - carried from 4c038cb | `tests/Api.Tests/Ai/RunWorkflowTests.cs:300-301` - `Assert.Equal(["Queued", "Running", "Succeeded", "Failed"], Enum.GetNames<WorkflowRunStatus>())` | PASS |
| C54 | columns + API row order | front batch, exit 0 - carried from 4c038cb | `src/web/src/app/features/ai/workflows-list.spec.ts:65` - `expect(headers).toEqual(['Nome', 'Nós', 'Atualizado', 'Ações'])`; `:69` | PASS |
| C55 | shared loading | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflows-list.spec.ts:85-86` - `list-loading` not null, `workflows-table` null | PASS |
| C56 | empty copy + Novo workflow | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflows-list.spec.ts:93` - `toContain('Nenhum workflow ainda')`; `:94` | PASS |
| C57 | error + retry repeats | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflows-list.spec.ts:111` - `toBe('Tentar de novo')`; `:114` - `expect(calls).toBe(2)` | PASS |
| C58 | `Desativar` -> confirm, cancel/confirm | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflows-list.spec.ts:132` - `toBe('Desativar')`; `:134-137`; `:146` | PASS |
| C59 | no manage hides new/deactivate | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflows-list.spec.ts:154-156` - `create-workflow`, `deactivate-wf-2`, `deactivate-wf-1` null | PASS |
| C60 | nav Workflows after Agentes, hidden without read | front batch, 3 tests - carried from 4c038cb | `src/web/src/app/shell/shell.spec.ts:163` - `toBe('Workflows')`; `:168`; `:180`; `:191` | PASS |
| C61 | nodes at x/y, labels, arrows | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:225-228` - `a.style.left` `'10px'`, `top` `'20px'`; `:233-234` | PASS |
| C62 | unique derived keys | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:244-245` - `node-triagem`, `node-triagem-2` not null | PASS |
| C63 | drag to (200,150), PUT sends it | front batch, 2 tests - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:261-262`; `:266` - `toMatchObject({ key: 'a', x: 200, y: 150 })`; `:282-283` | PASS |
| C64 | port click creates edge | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:320-321` - `edge-a-b` present, `edgeCount 1` | PASS |
| C65 | cycle / duplicate messages | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:330-332` - `'Esta ligação criaria um ciclo'`; `:336-338` - `'Ligação já existe'` | PASS |
| C66 | node panel, instruction, remove node+edges | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:348`; `:352` - `instruction: 'Resuma'`; `:359-361` | PASS |
| C67 | remove edge by button and Delete | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:375-376`, `:383-384` | PASS |
| C68 | save POST+navigate / PUT | front batch, 2 tests - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:406-408` - `navigate` `['/ai/workflows', 'wf-new']`; `:421-423` | PASS |
| C69 | 400 edges message, canvas kept | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:446` - `toBe('As ligações formam um ciclo.')`; `:447-451` | PASS |
| C70 | empty canvas copy, save disabled | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:458` - `toBe('Adicione um agente para começar')`; `:459` | PASS |
| C71 | read-only canvas | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:466-470` - palette, save, run-start, port null; `cdk-drag-disabled` | PASS |
| C72 | leave confirm | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:482-491` - `'Sair sem guardar as alterações?'` | PASS |
| C73 | 404 not found + back link | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:501-502` - `toContain('Workflow não encontrado')`; href `'/ai/workflows'` | PASS |
| C74 | Executar POST + run view; dirty disables | front batch, 2 tests - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:518-519` - `toEqual({ input: 'olá' })`; `:530-531` - `'Guarde antes de executar'` | PASS |
| C75 | default 2000 ms, stops at terminal and on destroy | front batch, 4 variants - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:535` - `toBe(2000)`; `:566-570`; `:596-597`; `:622` | PASS |
| C76 | 5 step labels, spinner on Running | front batch, 5 variants - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:639` - `toBe(label)`; `:640`; `:642` | PASS |
| C77 | step detail + errorCode | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:665-668` - `'Tokens: 3 in / 4 out'`, `'Custo: $0.001'`, `'Latência: 12 ms'`; `:671` - `'Timeout'` | PASS |
| C78 | final state, $0.003, duration; null -> — | front batch, 2 tests - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:680-682` - `'Concluído'`, `'$0.003'`, `'3.2 s'`; `:691` - `toBe('—')` | PASS |
| C79 | runs list columns, empty, open | front batch, 3 tests - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:702` - `toEqual(['Estado', 'Input', 'Custo', 'Início'])`; `:710`; `:722`; `:731` | PASS |
| C80 | 429 detail, input kept | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:750-751` - `toBe('Limite de pedidos de IA do tenant atingido.')` | PASS |
| C81 | run view uses run graph | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:764-767` - `node-a`, `node-b` present, `node-c` null | PASS |
| C82 | 8 routes in features.json and openapi, both ways | ArchitectureTests exit 0 at fd704c7 - carried from 4c038cb | `tests/ArchitectureTests/OpenApiContractTests.cs:75`, `:85` - `Assert.True(missing.Count == 0, ...)` | PASS |
| C83 | every route has a front client | front batch - carried from 4c038cb | `src/web/src/app/architecture.spec.ts:52` - `expect(missing).toEqual([])`; `:71` | PASS |
| C84 | no new front dependency | `git diff --exit-code 6c038f0 -- src/web/package.json` exit 0 at fd704c7 | `src/web/package.json:1` - file identical to base | PASS |
| C85 | no token -> 401 on 8 routes | Api.Tests batch, 8 cases - carried from 4c038cb | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:332` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C86 | editor loading before canvas | front batch - carried from 4c038cb | `src/web/src/app/features/ai/workflow-editor.spec.ts:216-217` - `editor-loading` not null, `canvas` null | PASS |
| C87 | three regions palette, canvas, panel (right) + labels `Adicionar agente`, `Guardar`, `Executar`, `Remover nó`, `Remover ligação` | front batch, `WorkflowEditor > arranjo: paleta, canvas e painel a direita, com os rotulos` passed - verified at fd704c7 | `src/web/src/app/features/ai/workflow-editor.spec.ts:300` - `expect(regions).toEqual(['palette', 'canvas', 'panel'])`; `:301-303` - palette `h2` `toBe('Adicionar agente')`; `:304` - `toBe('Guardar')`; `:305` - `toBe('Executar')`; `:308` - `toBe('Remover nó')`; `:310` - `toBe('Remover ligação')` | PASS |
| C88 | `description` 1001, `key` blank, `key` 51 -> 400 on `description`, `nodes`, `nodes` | Api.Tests batch, `Post_ShouldReturn400_ForBoundsBeyondPlan` 3 cases Aprovado - verified at fd704c7 | `tests/Api.Tests/Ai/CreateWorkflowTests.cs:102` - `response.StatusCode == HttpStatusCode.BadRequest`; `:104` - `problem.Errors.ContainsKey(key)`; cases `:87-89` - `new string('d', 1001)` -> `"description"`, `Node("", id)` -> `"nodes"`, `Node(new string('k', 51), id)` -> `"nodes"` | PASS |

The Swept rows that resolve to existing are carried from 4c038cb. `AgentLoop.cs` is still absent
from `git diff --stat 6c038f0..fd704c7`.

## Coverage

The usage-entry and validation-rules rows are verified at fd704c7, because the fix touched their
authority. The other rows are carried from 4c038cb, and their proofs were re-run green at fd704c7.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /workflows` statuses (5) | carried from 4c038cb | 201 C1 · 400 C2 C5 C88 · 401 C85 · 403 C13 · 404 C15 | - |
| `GET /workflows` statuses (4) | carried from 4c038cb | 200 C8 · 401 C85 · 403 C13 · 404 C15 | - |
| `GET /workflows/{id}` statuses (4) | carried from 4c038cb | 200 C9 · 401 C85 · 403 C13 · 404 C11 C15 | - |
| `PUT /workflows/{id}` statuses (5) | carried from 4c038cb | 200 C6 · 400 C7 · 401 C85 · 403 C13 · 404 C11 C15 | - |
| `DELETE /workflows/{id}` statuses (4) | carried from 4c038cb | 204 C10 · 401 C85 · 403 C13 · 404 C11 C15 | - |
| `POST .../runs` statuses (6) | carried from 4c038cb | 202 C18 · 400 C19 C20 · 401 C85 · 403 C13 · 404 C11 C15 C26 · 429 C21 C22 C23 | - |
| `GET .../runs` statuses (4) | carried from 4c038cb | 200 C50 · 401 C85 · 403 C13 · 404 C15 | - |
| `GET .../runs/{runId}` statuses (4) | carried from 4c038cb | 200 C48 · 401 C85 · 403 C13 · 404 C15 C51 | - |
| policy per route (8) | carried from 4c038cb | C14 table over 8 | - |
| run status (4) | carried from 4c038cb | Queued C18 · Running C28 · Succeeded C34 · Failed C35 C36 C44 C46 | - |
| step status (5) | carried from 4c038cb | Pending C18 · Running C44 · Succeeded C33 · Failed C35 · Skipped C35 C44 | - |
| step errorCode (4) | carried from 4c038cb - `WorkflowRunner.cs:30-32, 285` | exception name C35 · Timeout C36 · AgentUnavailable C37 · QuotaExceeded C38 | - |
| run errorCode (2) | carried from 4c038cb | Interrupted C44 · InternalError C46 | - |
| validation rules (15) | verified at fd704c7 - `CreateWorkflow.cs:38-87` + `RunWorkflow.cs:16` | 12 plan cases C2 · agent unknown/inactive C5 · input C19 · `description` > 1000, `key` blank, `key` > 50 C88 (`CreateWorkflowTests.cs:87-89`, `:102-104`) | - |
| usage entry per step outcome (5) | verified at fd704c7 - AC 28 x the endings in `WorkflowRunner.cs:254-286`, each reaching `TrackAsync` at `:290` | LLM success C41 (`WorkflowRunnerTests.cs:363`) · LLM exception C41 (`:370`) · AgentUnavailable C41 (`:374`) · Timeout C36 (`:233-236`, fault f killed) · QuotaExceeded C38 (`:277-280`, fault e killed) | - |
| message composition (3) | carried from 4c038cb | C32 x 3 | - |
| worker principal (2) | carried from 4c038cb | Admin C39 · no tool permission C39 | - |
| `Ai:Workflows` options (4) | carried from 4c038cb | MaxParallelSteps C30 C31 · StepTimeoutSeconds C36 · MaxRunMinutes C44 · PollIntervalSeconds C27 C47 | - |
| startup config (1 assembly) | carried from 4c038cb | C27 C31 | - |
| polling terminal states (2) | carried from 4c038cb | Succeeded C75 · Failed C75 | - |
| `nav-ai-workflows` visibility (3) | carried from 4c038cb | available + read C60 · no read C60 · AI unavailable C60 | - |
| editor 400 field keys (3) | carried from 4c038cb | edges C69 · name C69 · nodes C69 | - |
| `workflow-editor` regions + labels (3 regions, 5 labels) | verified at fd704c7 - AC 50, `plan.md:193` | palette/canvas/panel order C87 (`workflow-editor.spec.ts:300`) · 5 labels C87 (`:301-310`) | - |
| `workflows-list` states (4) | carried from 4c038cb | loading C55 · empty C56 · error C57 · no permission C59 | - |
| `workflow-editor` states (4) | carried from 4c038cb | loading C86 · empty C70 · error C69 C73 · no permission C71 | - |
| canvas step status (5) | carried from 4c038cb | C76 table over 5 | - |
| one-way doors (7) | carried from 4c038cb - `plan.md:304-310` | 1 C16 C17 · 2 C16 C17 C53 · 3 C24 C39 · 4 C25 C45 · 5 C27 C42 C43 C47 · 6 C84 · 7 C36 C38 C41 | - |

## Test policy rows

The `WorkflowRunner.cs` row was unmet in round 2 and is re-judged here, verified at fd704c7. The
fix touched only `WorkflowRunnerTests.cs`, which is that row's proof file. The other rows are
carried from 4c038cb.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decides, reached across a boundary | `WorkflowGraph` + the validator (`CreateWorkflow.cs:26`) | boundary C2 C3 C5 C7 C88 · own layer C4 | yes - carried from 4c038cb |
| Decides, reached across a boundary | `workflow-editor.ts` (via MSW) | C61-C81, C86, C87 | yes - carried from 4c038cb |
| Decides, not reached across a boundary | `WorkflowRunner.cs` | own layer C28-C47, boundary path C27 | yes - verified at fd704c7. The usage-entry table (AC 28) has all 5 rows asserted: `WorkflowRunnerTests.cs:233-236`, `:277-280`, `:361-374`. Faults d, e and f are all killed. |
| Entry point that decides nothing | `CreateWorkflow`/`UpdateWorkflow`/`RunWorkflow` handlers and slices | accepted + each rejected + each error path at the boundary | yes - carried from 4c038cb |
| Instrumentation, pass-throughs | `CurrentUserAccessor.cs`/`BackgroundPrincipal`, mappers | none of its own | yes - carried from 4c038cb |

## Faults injected

Verified at fd704c7. Only backend faults were needed, because the fix touched only the runner's
test surface.

**Setup.** The scratch tree came from `git worktree add <scratchpad>/wt HEAD`. The real tree's
`git status --porcelain` was recorded first: `?? .specs/features/workflows-agente/verification.md`,
the expected baseline. Each mutation was reverted with `git checkout -- <file>` inside the scratch
tree before the next one.

**Cleanup.** `git worktree remove --force`, then `git worktree prune`. Afterwards,
`git worktree list` shows only the main tree, and the real tree's porcelain is byte-identical to
the baseline (`diff` empty).

**How each fault was run.** Both faults ran against the same backend batch the Checks section
uses: `CreateWorkflowTests|RunWorkflowTests|WorkflowRunnerTests`, 93 tests.

Faults a-d were killed in round 2, and the code they mutate is unchanged since then. They are
carried from 4c038cb.

| Mutation | Location | Killed |
| --- | --- | --- |
| (e) QuotaExceeded branch returns early, before `TrackAsync` (`outcome = StepOutcome.Failed(...)` -> `return StepOutcome.Failed(...)`) - the round-2 survivor | `src/Api/Features/Ai/WorkflowRunner.cs:260` | yes - 1 of 93 fails: C38 `RunOnce_ShouldFailStepWithQuotaExceeded_WhenQuotaExhausted` (`Assert.Single() Failure: The collection was empty`, `WorkflowRunnerTests.cs:277`) |
| (f) skip `TrackAsync` on Timeout (`if (outcome.ErrorCode != TimeoutErrorCode) await ...TrackAsync(...)`) | `src/Api/Features/Ai/WorkflowRunner.cs:290` | yes - 1 of 93 fails: C36 `RunOnce_ShouldFailStepWithTimeout_WhenStepExceedsTimeout` (`Assert.Single() Failure: The collection was empty`, `WorkflowRunnerTests.cs:233`) |

## Gate

Verified at fd704c7.

- `dotnet test tests/Api.Tests`: 431 passed, 0 failed.
- `ng test --no-watch` (full front, run with `npx -y node@24.15.0 node_modules/@angular/cli/bin/ng.js`): 201 passed, 0 failed, 27 files.
- `dotnet test tests/ArchitectureTests --filter FullyQualifiedName~OpenApiContractTests`: 5 passed, 0 failed.
- `dotnet ef migrations has-pending-model-changes --project src/Api`: exit 0, no pending changes.
- `git diff --exit-code 6c038f0 -- src/web/package.json`: exit 0.

Remaining notes. None of these is a finding that fails the feature:

- The precision gaps listed under Binding sources: the claim text of C36, C38, C60, C63, C69, C75,
  C76 and C79 is narrower than what its proof asserts.
- `WorkflowRunnerTests.cs:236` asserts the Timeout latency as a lower bound (`>= 900`), not an
  exact value. The plan fixes no exact value.
