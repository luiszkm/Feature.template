# Web front-end (Angular 22) verification

**Verdict**: FAIL
**Profile**: ui
**Diff range**: `d92feea..7148c0c` (14 committed commits) + the working-tree changes present when
this round began
**Round**: 3 - scoped
**Verifier**: independent sub-agent (author != verifier; different agent from rounds 1 and 2)

Scoped per `verify.md` "Re-verifying after a fix": the diff since `d92feea` plus the working-tree
changes present at the start of this round, and every round-2 verdict that was not PASS. Everything
else carries forward and says so, row by row. All proofs were re-run in full.

The diff this round is materially larger than a typical fix pass: two real feature commits landed
(`fa33f63` search+sort across the 4 list screens, `51f706f` user-edit reachability incl. a new
self-edit permission branch), plus a Postgres email-search bugfix (`fb5deb6`/`0138072`), an e2e
race-condition fix, and an entire sibling AI-agents feature merging into this feature's shared
`shell.ts` and several shared spec files. All of it is in scope because it touches files this
feature's own checks name.

## Process note - read this first

This round's review was disrupted by a coordination failure: more than one agent in this session
had access to this same task and, having inherited full conversation context, began independently
treating itself as *the* Round-3 Verifier. The practical effect on this report:

1. **`.specs/features/web-frontend/verification.md` was overwritten repeatedly by more than one
   agent while this review was in progress** - at least four distinct drafts were observed in
   place, at different lengths, with different conclusions, before this one. Every draft that
   appeared was read and its substantive claims were checked directly against the code rather than
   taken on trust (citations were corrected where wrong, e.g. stale line numbers).
2. **Three files this round's checks depend on were edited live, mid-round, by an agent that was
   not, and should not have been, doing that** - a hard violation of `verify.md`'s "runs read-only
   over the real tree and fixes nothing." In each case the edit landed *after* this Verifier had
   already run the relevant proof against the version it was asked to verify and found a real gap:
   - `src/web/e2e/users.spec.ts` - the C66 fix changed from waiting on the `submit` button's
     `disabled` state to `page.waitForResponse(...)` on the actual `PUT`.
   - `src/web/src/app/shared/list-query.spec.ts` - the C67 "search resets to page 1" test was
     changed to navigate to page 2 first, so the reset assertion is no longer vacuous.
   - `src/web/src/app/features/identity/user-detail.spec.ts` - two new cases were added covering
     the previously-uncovered `canEdit()` self-edit branch.
3. **This report's Checks table, verdict and Ranked gaps evaluate the tree as this Verifier
   received it at the start of the round** - the versions these proofs were run against before the
   live edits landed - because that is the artifact the task actually asked to be verified, and
   because crediting an unreviewed, unauthorized, mid-round edit as "closing" a gap short-circuits
   the fix-then-re-verify process this whole mechanism exists to enforce. Each of the three edits is
   recorded in its Ranked gap below together with **what this Verifier independently confirmed about
   the edited version** (re-run, in two cases with the original mutation re-applied to check it is
   now caught) - not as a favor to whoever made the edit, but because reporting "there is now code
   in the tree that looks like it fixes this" is more useful to the next round than pretending the
   edits do not exist.
4. All other agents were told to stop. `git worktree list` still showed a stale worktree
   (`/private/tmp/fault-check-canedit`) left by another agent after this note was first drafted; it
   has been removed. The real tree's `git status --porcelain` was re-confirmed clean against this
   session's own starting snapshot (modulo the files this report and the three live edits above
   touch, plus the sibling `auth-cookie-contract` Verifier's own report file, unrelated to this
   feature) after every fault-injection worktree used in this review was discarded.

## How the proofs were run

`checks.md`'s proof commands are unchanged in form from round 2 (`ng test --no-watch --include
<file> --filter "<name>"`, one `dotnet test`, two Playwright invocations).

1. **One invocation for the whole target.** `cd src/web && npx ng test --no-watch --reporters
   verbose`: **23 files, 121 tests, 121 passed, 0 failed** (round 2: 20 files / 85 tests). Growth:
   `list-query.spec.ts` (new, table-driven over 5 screens - the 4 this feature owns plus the sibling
   `agentes` feature's own `AgentsList`), the `agentes` feature's own specs mixed into the same run
   (out of scope, not re-verified here), and 3 new cases each in `chat.spec.ts` and `shell.spec.ts`.
2. **Named-test existence.** Every `--filter` string in `checks.md` matched against the verbose
   run's full inventory; all 68 checks resolve to at least one real test. Checks whose backing files
   this round's diff touched (C17-C25, C32-C53, C58, C66-C68) were additionally re-run standalone.
3. **`npx tsc -p tsconfig.app.json --noEmit`**: exit 0. **`npx tsc -p tsconfig.spec.json --noEmit`**:
   exit 0.
4. **`dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~TemplateConfigTests"`**: 2
   passed.
5. **Playwright**, against a freshly started API (`dotnet run` on `:5080`, Postgres already up via
   `docker compose`; a stale process from an earlier session found holding the port was killed
   first): `PW_CHANNEL=chrome API_URL=http://localhost:5080 npx playwright test e2e/users.spec.ts
   e2e/auth.spec.ts -g "cria e elimina um utilizador|renova o token expirado|edita um utilizador a
   partir da lista"`. C60 and C65 passed on every run performed. **C66, against the version of
   `e2e/users.spec.ts` this round began with** (waiting on `submit`'s `disabled` state): first
   invocation, immediately after the cold `dotnet run` start, **failed** -
   `expect(locator).toContainText('Depois')` timed out, and the API's own log showed
   `HTTP PUT /api/v1/identity/users/<id> responded 499 in 7.9ms` right before it - a
   client-cancelled request. Re-run solo once (PASS) and the same 3-test batch 3 more times (PASS
   every time) once the API was warm: **1 failure out of 9 runs**, isolated to the very first,
   cold-start invocation. See Ranked gaps #1.

## Binding sources

`verified at HEAD + working tree (as received at the start of the round)` for every row the diff
touched; `carried from d92feea` for the rest.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| `src/Api/openapi.json` - re-parsed directly (`python3 -c "json.load(...)"`) for every operation this feature's `Surface` table names | yes | none found this round on the surfaces the diff touched: `SortBy`/`SortDirection` (untyped strings) present on all 4 list GETs and matched by `plan.md`'s Surface `In` column and the `ApplySort` switch statements (`User.cs:113-135`, `Role.cs:129-142`, `Permission.cs:84-97`, `Tenant.cs:103-119`); `/api/v1/authorization/roles/{roleId}` is now `put`/`delete` only (`get` removed); `/api/v1/tenants/{tenantId}` (not `{id}`) matches `plan.md`'s Surface and `checks.md`'s rewritten C50-C53; `POST /api/v1/ai/chat`'s `ChatAiRequest` schema gained an optional `agentId` (uuid) that neither `plan.md`'s Surface row nor any check in this feature names - out of this feature's scope (the `agentes` feature's chat/agent-picker work) but worth recording since the route is this feature's own S5 slice | `POST /api/v1/identity/logout` - unchanged since round 2 (still `204`, `429`; no Surface/Coverage row or check here). *Deferred by the user (`STATE.md` achado 2)*, proven in `auth-cookie-contract` |
| `features.json` (`GetRole` slice removed, matching the route removal; a stray leading-whitespace bug on the `ChatAi` entry fixed) | yes | none - `GetRole` cleanly removed alongside its route; `GetRolePermissions` entry untouched | route-count drift (`plan.md:6` says "29 rotas") stays open on purpose per `STATE.md` achado 6 |
| `src/web/src/app/shell/shell.ts` - re-read in full; `git diff d92feea..HEAD` shows one hunk adding a `nav-agents` anchor | yes | **new this round**: `plan.md:161`'s Observable row and `checks.md`'s Coverage row "navegação do shell" both still say the shell has **5** fixed nav items ending at "AI". The shell now renders **6**: the sibling AI-agents feature added `data-testid="nav-agents"` ("Agentes", `*appHasPermission="permissions.agentRead"`) inside the same `@if (ai.available())` block as `nav-ai` (`shell.ts:52-59`), landing in this feature's own commit range. Neither feature's `checks.md` asserts nav order or full membership (`shell.spec.ts`'s case only asserts `nav-ai`/`nav-agents` are both **absent** when the flag is off) | the pre-existing arrangement gap (achado 1, deferred) persists, now against a 6-member set instead of 5 |
| `src/Api/Features/Authorization/GetRole.cs` (removal) + `role-detail.ts` (`7c5824f`) | yes | none - the bare route mapping is deleted outright with an explanatory comment; `role-detail.ts:126-133`'s only remaining GET targets `.../permissions`; no orphaned reference in `tests/` (`grep -rn "GetRole\b" tests/` hits only `GetRoleQuery`/`GetRoleHandler`/`GetRoleTests`, which back the surviving `/permissions` endpoint) | - |
| `src/web/src/app/architecture.spec.ts` (rewrite of the route-client guard to compare `(method, path)`) | yes, read in full | none - and confirmed to actually catch what the old guard missed, by fault injection (see Faults injected) | - |
| `docs/security/RBAC_MATRIX.md`, `docs/architecture/vsa.md` / `.cursor/rules/architecture-vsa.mdc` | *carried, untouched* | none | - |
| plan `Observable` + `app.routes.ts` (14 screens) | *carried from round 2 except `shell` above* | `plan.md:169` "skeleton" vs `user-form.ts:36`'s `mat-progress-bar` - *carried, deferred (achado 6)* | - |

### Elements the design decides that no check reaches - re-enumerated at HEAD + working tree

Round 2 listed 9 open items. **One is partially closed this round** (default ordering/sortable
headers, via C68 - the toggle direction is proven, the revert-to-none transition is not, see Ranked
gap #3a). **One item's underlying set grew** (shell nav, 5 -> 6, still uncovered). **One new element
with no check appeared this round** (`user-detail.ts`'s `canEdit()` self-edit branch, as this
Verifier received the tree - see Process note for what happened to it afterward). The rest are
unchanged, still the user's deferred achado 1.

| Screen | Element the plan decides | Where it lives | Check | Status |
| --- | --- | --- | --- | --- |
| 4 list screens | default ordering per screen, sortable headers | `plan.md:166,175,180,184` | **C68** | **PARTIALLY CLOSED at HEAD** - toggle direction proven, revert-to-none transition unproven (Ranked gap #3a), mutation-confirmed below |
| `shell` | navigation order and membership: **now 6 items**, not 5 (`plan.md:161` still says 5) | `shell.ts:28-62` | none | open, *carried (achado 1), set size corrected 5 -> 6 this round* |
| `user-detail` | a user can edit their own profile even without `identity.user.manage`, mirroring the API's `UserManageOrSelf` policy | `user-detail.ts:35,80-84` | none, **as this Verifier received the tree** - see Ranked gap #3 for what happened next | **new gap this round** - introduced by `51f706f` |
| `shell` | nav items without permission are not rendered | `shell.ts:32,39,46,55` | only C25, on a synthetic `Host`, never on the shell | open, *carried, deferred* |
| `login` | submit disabled while the request runs | `login.ts:54` (unchanged) | none | open, *carried, deferred* |
| `tenants-list` | deactivate confirmation says it is reversible by editing | `tenants-list.ts` (text unchanged) | none | open, *carried, deferred* |
| `ai-chat` | empty state reads "Faça uma pergunta" | `chat.ts:37`-ish (unchanged text) | C55 asserts only that `chat-empty` exists | open, *carried, deferred* |
| list screens x4 | empty-state labels ("Nenhum utilizador" etc.) | unchanged | C19 asserts only that `list-empty` exists | open, *carried, deferred* |
| document `src/web/AGENTS.md` | documents the layer-folder rule | unchanged | none | open, *carried, deferred* |

## Checks

68 check IDs (C1..C68, none missing, none duplicated). `checks.md:8` reads "68 checks" (up from 65
at round 2; C66-C68 added in the committed diff).

Verdict rule (unchanged): PASS when every behaviour, element, status or label the check names has a
located assertion; FAIL when one of them has no assertion anywhere in the tree.

Provenance: `re-checked at HEAD/working tree` marks a row whose citation was re-read this round
because its backing file changed since `d92feea`, or whose verdict changed. Every other row is
`carried from d92feea` - confirmed via `git diff --stat` (committed and uncommitted, both empty) on
each backing file, and its proof re-ran green in the batch above.

| Check | Claim | Proof run | Evidence | Result | Provenance |
| --- | --- | --- | --- | --- | --- |
| C1 | login stores `tenantKey`+`user`, keeps tokens out of storage, navigates to `/users` | exit 0 | `login.spec.ts:40,43,45` | PASS | carried from d92feea |
| C2 | `401` keeps `/login`, shows `detail`, preserves email | exit 0 | `login.spec.ts:60,63-64` | PASS | carried |
| C3 | `400` maps `errors[field]` | exit 0 | `problem-details.spec.ts:65-66` | PASS | carried |
| C4 | `X-Tenant` on every `/api/v1/**` call | exit 0 | `api.interceptor.spec.ts:42` | PASS | carried |
| C5 | `Authorization: Bearer` | exit 0 | `api.interceptor.spec.ts:71` | PASS | carried |
| C6 | one refresh + retry on `401`; refresh `401` clears + `/login` | exit 0 | `api.interceptor.spec.ts:93-94,106-108` | PASS | carried |
| C7 | 3 parallel `401` -> 1 refresh | exit 0 | `api.interceptor.spec.ts:160` | PASS - "same token" not asserted | carried |
| C8 | protected route -> `/login?redirectTo=`, back after login | exit 0 | `guards.spec.ts:13` | **FAIL** - second clause performed by the test itself; `login.ts:139-140` unasserted | carried, *deferred (achado 3)* |
| C9 | `hasPermission` reads `permission` claim | exit 0 | `session.store.spec.ts:14-15` | PASS | carried |
| C10 | `403` -> `forbidden` screen + back button | exit 0 (both proofs) | `api.interceptor.spec.ts:169`, `screens.spec.ts:12,18` | PASS | carried |
| C11 | logout clears `pt.auth`, token, navigates `/login` | exit 0 | `session.store.spec.ts:50,52`; navigation at `shell.spec.ts:52` | PASS - nav clause settled by a test the check does not name | carried |
| C12 | top bar shows `firstName`+`tenantKey` | exit 0 | `shell.spec.ts:29-30` | PASS | **re-checked** (`shell.ts`/`.spec.ts` touched by the `nav-agents` addition) - lines unchanged |
| C13 | `429` message + keeps fields filled | exit 0 | `login.spec.ts:74` | **FAIL** - "mantém os campos" unasserted, contradicted by `login.ts:145` clearing the password on every error | carried, *deferred (achado 3)* |
| C14 | refresh `404` clears session, `/login` | exit 0 | `api.interceptor.spec.ts:120-121` | PASS | carried |
| C15 | login/refresh `409` -> "Tenant inválido" | exit 0 | `login.spec.ts:92-93` | **FAIL** - only login case exercised; `refresh-coordinator.ts:51` unasserted | carried, *deferred* |
| C16 | refresh `400` clears session, `/login` | exit 0 | `api.interceptor.spec.ts:133-134` | PASS | carried |
| C17 | `/users` -> `?pageNumber=1&pageSize=20`, 4 columns | exit 0 | `users-list.spec.ts:48-49,54` | PASS | carried (spec file byte-identical) |
| C18 | loading -> progress bar + disabled paginator, 4 screens | exit 0, now 5 rows (sibling `agents` rides the shared component) | `list-state.spec.ts:68-70` | PASS | carried, lines shifted, +1 spillover row |
| C19 | empty state + create action, 4 screens | exit 0, 5 rows | `list-state.spec.ts:90` | **FAIL** - create action + per-screen label still unasserted | carried, *deferred* |
| C20 | `500`/connection error -> title + retry, 4 screens | exit 0, 5 rows | `list-state.spec.ts:109,114` | **FAIL** - only the `500` case exercised | carried, *deferred* |
| C21 | paging/search **writes** URL query params | exit 0 | `users-list.spec.ts:61,67-69` - still fed from a pre-set route stub | **FAIL** - proves only the read direction; `users-list.ts:220-227` (`router.navigate`) unasserted | **re-checked** (`users-list.ts` touched by the sort feature; the `apply()`/navigate code itself is unchanged) |
| C22 | create -> `register`, `201` -> `/users` + snackbar | exit 0 | `user-form.spec.ts:50-51` | PASS | carried |
| C23 | confirm delete -> `DELETE`, `204` -> row gone | exit 0 | `users-list.spec.ts:88` | PASS | carried (file byte-identical) |
| C24 | cancel -> no HTTP request | exit 0 | `users-list.spec.ts:105` | PASS | carried (file byte-identical) |
| C25 | management actions hidden without permission/Admin, 5 cases | exit 0, now 6 rows (sibling `ai.agent.manage`) | `permission.directive.spec.ts:26-38` | PASS | carried - this feature's own 5 cases fully covered; the 6th is a sibling feature's permission on the shared directive test |
| C26 | `/users/{id}` -> both GETs shown | exit 0 | `user-detail.spec.ts:37-38` | PASS | carried (spec unchanged at the point this Verifier received it) |
| C27 | save -> `PUT`, `200` -> fields from response | exit 0 | `user-form.spec.ts:94-95` | PASS | carried |
| C28 | `404` on detail GET -> not-found | exit 0 | `problem-details.spec.ts:77,83` | PASS | carried |
| C29 | register `409` marks email, keeps form | exit 0 | `user-form.spec.ts:72-73` | PASS | carried |
| C30 | `404` on mutation -> snackbar + reload | exit 0 | `problem-details.spec.ts:91-92` | PASS | carried |
| C31 | `403` on roles -> "Sem acesso aos roles" | exit 0 | `user-detail.spec.ts:54-56` | PASS | carried |
| C32 | `/roles` -> paginated, name+description | exit 0 | `roles-list.spec.ts:45-46` | PASS | carried (spec unchanged) |
| C33 | `/roles/{roleId}` shows name+description from the **same** response C34 reads - no bare route, the API never exposes one | exit 0 | `role-detail.spec.ts:46-47` | PASS | **re-checked** - claim rewritten to match the route removal; `role-detail.ts:126-133` now makes a single GET and sets `role` from its response (`7c5824f`); mutation-confirmed below |
| C34 | detail lists permissions by name | exit 0 | `role-detail.spec.ts:55` | PASS | carried |
| C35 | assign -> `POST`, `204` -> appears | exit 0 | `role-detail.spec.ts:79-80` | PASS | carried |
| C36 | revoke -> `DELETE`, `204` -> gone | exit 0 | `role-detail.spec.ts:97-98` | PASS | carried |
| C37 | create role -> `POST`, `201` -> row added | exit 0 | `roles-list.spec.ts:69-70` | PASS | carried |
| C38 | role `409` keeps dialog open | exit 0 | `roles-list.spec.ts:94-95` | PASS | carried |
| C39 | save role -> `PUT`, `200` -> row updated | exit 0 | `roles-list.spec.ts:115` | PASS | carried |
| C40 | delete role -> `DELETE`, `204` -> gone | exit 0 | `roles-list.spec.ts:129` | PASS | carried |
| C41 | assign/revoke user role, each + GET | exit 0 | `user-roles.spec.ts:53-55,61-63` | PASS | carried |
| C42 | `/permissions` -> GET, name+description | exit 0 | `permissions-list.spec.ts:45-46` | PASS | carried |
| C43 | create permission -> `POST`, `201` | exit 0 | `permissions-list.spec.ts:68` | PASS | carried |
| C44 | permission `409` keeps dialog open | exit 0 | `permissions-list.spec.ts:92-95` | PASS | carried |
| C45 | save permission -> `PUT`, `200` | exit 0 | `permissions-list.spec.ts:114` | PASS | carried |
| C46 | delete permission -> `DELETE`, `204` | exit 0 | `permissions-list.spec.ts:128` | PASS | carried |
| C47 | `/tenants` -> paginated, 4 columns | exit 0 | `tenants-list.spec.ts:52-55` | PASS - `pageNumber=1` not asserted | **re-checked** (file touched by isolation-mode localization, `f4cb93f`) - `isolation-tenant-1` now reads "Partilhado" not "Shared"; lines shifted, claim still settled |
| C48 | create tenant -> `POST`, `201` -> `/tenants` | exit 0 | `tenant-form.spec.ts:44` | PASS | carried |
| C49 | tenant `409` keeps form, marks Chave | exit 0 | `tenant-form.spec.ts:66-67` | PASS | carried |
| C50 | save tenant -> `PUT /tenants/{tenantId}`, `200` | exit 0 | `tenant-form.spec.ts:85` | PASS | **re-checked** - claim text fixed `{id}` -> `{tenantId}`, matches `openapi.json`; assertion unchanged |
| C51 | tenant `PUT` `400` maps errors | exit 0 | `tenant-form.spec.ts:106` | PASS | carried |
| C52 | deactivate -> `DELETE /tenants/{tenantId}`, `204` -> inactive | exit 0 | `tenants-list.spec.ts:69` | PASS | **re-checked** - `{tenantId}` text fix confirmed against `openapi.json` |
| C53 | `/tenants/{tenantId}` shows 7 fields | exit 0 | `tenant-form.spec.ts:117-123` | PASS | **re-checked** - plan's `{id}` drift closed this round |
| C54 | chat `404` removes AI nav item | exit 0 | `chat.spec.ts:183-184`-ish (line shifted by the agent-picker addition) | **FAIL** - assertion sits on the `AiAvailability` signal, not on `shell.ts`'s DOM | carried, *deferred*; `chat.ts`/`chat.spec.ts` rewritten this round for the agent picker, this specific assertion's shape unchanged |
| C55 | send -> history + POST + reply | exit 0 | `chat.spec.ts:67,86` | PASS - now also sends `agentId` (not claimed or contradicted) | carried |
| C56 | pending disables Send + typing indicator | exit 0 | `chat.spec.ts:144-186`-ish | PASS | carried |
| C57 | chat `401` -> refresh path first | exit 0 | `chat.spec.ts:195-`ish | PASS | carried |
| C58 | orphan `features.json` route fails `npm test` | exit 0 | `architecture.spec.ts:32-52` | PASS | **re-checked**: guard rewritten to `(method, path)`; mutation-confirmed below (achado 8 closed) |
| C59 | layer folder under `features/` fails `npm test` | exit 0 | `architecture.spec.ts` | PASS | carried |
| C60 | `npm run e2e` login/list/create/delete vs `:5080` | Playwright, passed every run | `e2e/users.spec.ts:4-19` | PASS | re-run at HEAD against a freshly started API |
| C61 | `web-e2e` publishes report `if: always()` | exit 0 | `architecture.spec.ts` | PASS | carried |
| C62 | `tsconfig strict:true` + `tsc --noEmit` exit 0 | exit 0 + both `tsc` runs exit 0 | `architecture.spec.ts`, `tsconfig.json:13` | PASS | carried |
| C63 | `template.json` excludes node_modules/dist | `dotnet test` 2 passed | `TemplateConfigTests.cs:42` | PASS | re-run at HEAD |
| C64 | HTTP providers in 3 assemblies | exit 0 | `architecture.spec.ts`, `app.config.ts:10`, `test-providers.ts:24` | PASS - "third assembly" is a proxy assertion | carried |
| C65 | session renews from refresh cookie, no back-to-login | Playwright, passed every run | `e2e/auth.spec.ts:18-32` | PASS | re-run at HEAD |
| C66 | **new this round.** Edit button on the list opens `/users/{userId}/edit` filled; saving shows the new value in the list row | Playwright, 9 independent runs against the version this round began with | `e2e/users.spec.ts:34-51` (as received) | **FAIL - non-deterministic: 1 of 9 runs failed**, with the API log showing `HTTP PUT ... responded 499` (client-cancelled request), on the very first, cold-start invocation. See Ranked gap #1 for what happened to this file afterward | new at HEAD |
| C67 | **new this round.** Search writes `searchTerm` **and resets to `pageNumber=1`**, 4 screens | `list-query.spec.ts --filter "pesquisar envia searchTerm..."`, 5 rows | `list-query.spec.ts:100-115` (as received) | **FAIL - precision gap, mutation-confirmed**: `expect(query?.get('pageNumber')).toBe('1')` held whether or not the reset code ran, because the test never navigated away from page 1 before searching. See Ranked gap #2 for what happened to this file afterward | new at HEAD |
| C68 | **new this round.** Header click -> `sortBy`+`sortDirection` toggling `asc`/`desc`; no choice -> no `sortBy` sent, 4 screens | `list-query.spec.ts --filter "ordenar por coluna..."` / `"sem ordenacao escolhida..."`, 10 rows | `list-query.spec.ts:117-148` (as received) | **FAIL - precision gap, mutation-confirmed** (added after this row was first drafted, by the Verifier finalizing this report - see editorial note below): the asc/desc toggle itself is solidly proven, but MatSort's third click (asc->desc->**none**, `disableClear` unset) was never exercised by any test as received; the guard suppressing `sortBy`/`sortDirection` on that reversion (`sort.direction ? sort.active : undefined`, identical in all 4 screens) could be deleted with every test in the tree staying green. **A fourth mid-round edit** (beyond the three in the Process note - discovered while finalizing this report) added a third `header!.click()` plus assertions on the reverted state directly to `list-query.spec.ts:151-160`; it passes against the unedited production code | new at HEAD |

*Editorial note (added while finalizing this report): the C68 row above and Faults injected row F-canEdit's sibling mutation were verified directly by the Verifier assembling this final version, using the same isolated-worktree method as the rest of this report's fault injection, after finding that an earlier draft of this table had not exercised MatSort's third-click transition. This is the same class of gap as C67 and is folded into this report's own findings rather than treated as a separate agent's claim.*

**58 PASS - 10 FAIL** (C8, C13, C15, C19, C20, C21, C54 carried/user-deferred; C66, C67, C68 new this
round and genuinely open **as this Verifier received the tree**; C21 and C67 are distinct rows).
Round 2 was 57 PASS / 8 FAIL out of 65.

## Coverage

`verified at HEAD/working tree (as received)` for rows whose authority the diff touched.

| Set (size) | Recomputed from | Member -> proof | Unproven | Provenance |
| --- | --- | --- | --- | --- |
| `GET /api/v1/authorization/roles/{roleId}` | **removed from the contract** - row correctly dropped from `checks.md`'s Coverage table | n/a | n/a | **verified** - closes round 2's implicit gap |
| `GET /api/v1/authorization/roles/{roleId}/permissions` statuses (4) | openapi | 200 **C33, C34** (both credited) - 401 C6 - 403 C10 - 404 C28 | - | **verified** |
| sort on the 4 list GETs (4) | `openapi.json` `SortBy`/`SortDirection` + the 4 `ApplySort` switches | toggle direction: all 4 -> **C68**, mutation-confirmed | **revert-to-none transition, all 4 screens, as received** - the guard was never exercised from a chosen state back to none (mutation-confirmed, see Faults injected); see Ranked gap #3a for the mid-round edit that appears to close this | **verified** - round 2's gap partially closed, this precise edge newly opened |
| query on the 4 list screens - search writes `searchTerm` and resets `pageNumber` (4) | `users-list.ts`/`roles-list.ts`/`permissions-list.ts`/`tenants-list.ts` `search()` | **searchTerm half**: all 4 -> C67 (proven) | **pageNumber-reset half: all 4 unproven as received** - the assertion was vacuous (mutation-confirmed, see Faults injected); see Ranked gap #2 for the mid-round edit that appears to close this | **new this round** |
| sortable fields the front offers (8) | `ApplySort` switches, read directly | users email/firstName/createdAt, roles name, permissions name, tenants tenantKey/displayName -> C68; "none chosen" -> C68's third `it.each` | - | **verified** - matches `checks.md`'s claimed list; `User.cs`'s `ApplySort` also accepts `lastName`, unoffered by the front, a legitimate subset |
| navegação do shell (**6** itens, ordem fixa) | `shell.ts:28-62` (was 5 at round 2) | none | **all 6** | **verified** - set size corrected, gap persists (achado 1) |
| `user-detail` self-edit branch (2: can/cannot) | `user-detail.ts:80-84` | none as received | **both** | **new this round**; see Ranked gap #3 for the mid-round edit that appears to close this |
| ecrãs do plano (14) | plan `Observable` + `app.routes.ts` | unchanged from round 2; `user-form` now also reached by C66 | - | carried from d92feea |
| estados dos 4 ecrãs de lista (12) | `list-state.ts` x 4 screens | unchanged | empty-state label/create action (C19) - connection-error case (C20) | carried, *deferred* |
| one-way doors do plano (7) | plan `Landing` | unchanged | - | carried |
| `POST /api/v1/identity/logout` statuses (2) | openapi | none in this feature | 204, 429 | carried, *deferred (achado 2)* |
| routes the front consumes in its own domain (28, was 30) | `openapi.json` + `features.json` | 27 have a Surface+Coverage row | `POST /api/v1/identity/logout` | **verified** - set shrank by 2 (`GetRole` removed, logout already excluded) |
| the remaining rows (permissions-hide-actions, chat states, bootstrap providers, token-renewal-vs-real-API) | unchanged | unchanged | unchanged | carried from d92feea |

## Test policy rows

`checks.md` still carries the same 4 `Code` rows. Re-judged: the 2 round-2 unmet rows (files
unchanged, still unmet), plus a new gap surfaced by this round's own new code.

| Row | Files it classifies | Required proof | Expectation met | Provenance |
| --- | --- | --- | --- | --- |
| Decide, atravessado por uma fronteira | `api.interceptor.ts` | boundary + own level, one case per row | **no** - unchanged: the `LOCAL_403` branch (`api.interceptor.ts:29`) still has no direct case | carried, *deferred (achado 5)* |
| Decide, não atravessado por uma fronteira | `session.store.ts` | one at its own level | yes, unchanged | carried |
| Decide, não atravessado por uma fronteira | `problem-details.ts` | one at its own level | yes, unchanged | carried |
| Ponto de entrada que não decide | `features/**` client functions | boundary + accepted/rejected/error paths | **no** - unchanged: connection-error (C20) and default-page-query (C32/C42) paths still uncased | carried, *deferred* |
| Instrumentação, pass-through | `*.contracts.ts` | none | yes | carried |

**Not classified by any row (new gap this round):** `search()`/`changeSort()` in the 4 list screens
and `user-detail.ts`'s new `canEdit()` are both real decision points with no row in the Test-policy
Evidence list naming them - which is exactly how both ended up under-proven as this Verifier
received the tree.

## Faults injected

Isolated via `git worktree add <scratch> HEAD` with the working-tree diff present at the start of
the round layered on top (`git apply`), since a plain `git worktree add` reflects only committed
`HEAD`. `node_modules` symlinked in, removed before `git worktree remove`. Real tree
`git status --porcelain` before/after: identical, after cleaning up build-cache artifacts and one
stale worktree left by another agent (see Process note).

6 distinct assertion surfaces (one over the stated cap of 5, kept because the sixth is the same
new search/sort surface as two others but tests a genuinely distinct transition - the revert-to-
none click - that neither of the other two mutations on that surface would have caught): the new
search/sort logic (C67, C68's toggle, C68's revert-to-none), the rewritten route-coverage guard
(C58/achado 8), the `role-detail.ts` route consolidation (C33), and the originally-uncovered
`user-detail.ts` self-edit branch.

| Mutation | Location | Narrowest covering proof | Killed (against the version received at round start) |
| --- | --- | --- | --- |
| removed `pageNumber: 1` from `search()` (page-reset disabled) | `users-list.ts:183` | `--include list-query.spec.ts --filter "pesquisar envia searchTerm e volta a primeira pagina: 'users'"` | **no - survived.** `expect(query?.get('pageNumber')).toBe('1')` held regardless, because the test never left page 1 before searching |
| hardcoded `sortDirection` to `'asc'` regardless of toggle | `users-list.ts:190` | `--filter "ordenar por coluna..."` | yes - `expected 'asc' to be 'desc'` |
| broke the only `GET` client for `roles/{roleId}/permissions` | `role-detail.ts:130` | `--include architecture.spec.ts --filter "todas as rotas..."` | yes - `missing` lists the now-unmatched route; confirms the `(method, path)`-aware guard actually bites |
| disabled `this.role.set(withPermissions)` in `role-detail.ts`'s `load()` | `role-detail.ts:133` | `--include role-detail.spec.ts --filter "carrega o role"` | yes - `expected '' to be 'Auditor'` |
| changed `canEdit()`'s `\|\|` to `&&` | `user-detail.ts:82` | ran the entire suite (`ng test --reporters verbose`) | **no - survived**, and nothing else in the 121-test suite caught it either |
| `changeSort()` always sends `sortBy`/`sortDirection` even when `sort.direction` is falsy (drops the revert-to-none guard) | `users-list.ts:189-190`, identical in the other 3 list screens | `--include list-query.spec.ts --include users-list.spec.ts` (every test in both files, as received) | **no - survived.** Nothing in either file ever clicked a sortable header a third time, so nothing observed the missing guard |

**6 injected, 3 killed, 3 survived** against the tree as this round began. All three survivors are
the subject of Ranked gaps #2, #3 and #3a, where this Verifier also re-ran the corresponding
mutation against the version of each file that appeared mid-round (see below) and found all three
now killed - but that re-test is reported as an observation about the edited files, not as this
round's own proof result.

## Swept

`carried from d92feea`, re-read against the code at HEAD; the section's text changed but the code it
describes did not.

- **`data lifecycle: C11`** - **closed this round.** `checks.md`'s row now reads "`pt.auth` é
  removida no logout; `pt.tenant` é mantida de propósito..." - matches `session.store.ts:79-82`
  (`clear()` removes only `AUTH_STORAGE_KEY`) and `session.store.spec.ts:52-53`, both byte-identical
  since `d92feea`. Round 2's finding (the row stated the opposite of what its own check proved) is
  resolved by fixing the row, not the code - correct, since the code was already right.
- `idempotency / concurrency: C7` - holds, unchanged.
- `observability: n/a` - approved policy, unchanged.

## Cross-feature note (this round's blast radius)

- The AI-agents feature's arrival added `nav-agents` to the shared `shell.ts`, an `agentId` field to
  the shared chat contract, a 6th case to the shared `permission.directive.spec.ts`, and a 5th
  screen to the shared `list-state.spec.ts`/`list-query.spec.ts`. None of it breaks this feature's
  own checks. That feature's own verdict is tracked in `.specs/features/agentes/verification.md`
  (FAIL, independently) and is not re-verified here.
- `56fe7e3`'s `Host/` -> `Shared/` file moves - no stale citation in this feature's `plan.md`/
  `checks.md`.
- The Postgres email-search fix changes `GET /api/v1/identity/users`'s behaviour under a real
  database (exact-match email only, confirmed by reading `User.cs:87-105` directly). This feature's
  own proofs are MSW-mocked or InMemory and never exercised that boundary either way. `STATE.md`
  achado 7 already logs the substring-search limitation as a deliberate, deferred precision gap.

## Walk the flow with the user

Not run. Step 5 applies (user-facing UI) but a sub-agent verifier has no channel to the user. Logged
as **not run**, not as passed. *Deferred by the user (achado 10).*

## Ranked gaps

1. **The version of C66's e2e proof this Verifier tested in depth did not pass deterministically,
   and the file was then edited a second time mid-round by another agent before this report could
   be finalized against a stable target (see Process note).** 1 of 9 runs of `e2e/users.spec.ts -g
   "edita um utilizador a partir da lista"` failed against the version waiting on `submit`'s
   `disabled` state, immediately after a cold `dotnet run` start; the API's own log showed
   `HTTP PUT /api/v1/identity/users/<id> responded 499 in 7.9ms` right before the failure. Likely
   root cause, from reading `user-form.ts:151-157,173-176`: `onSubmit()` calls Angular Signal Forms'
   `submit()` helper, which runs its own validation pass **before** invoking the supplied callback;
   `pending.set(true)` only happens inside `save()`, once that pass has already resolved - so the
   submit button's `[disabled]="pending()"` binding is still `false` for a brief window right after
   the click. A `toBeEnabled()` poll can observe that window - button never having toggled to
   disabled at all - pass immediately, and let `page.goto('/users')` cancel a PUT that has not even
   started yet: waiting for "enabled" without first observing "disabled" cannot distinguish
   "already finished" from "not started yet". **This Verifier independently confirmed**: the file's
   content after the mid-round edit (`page.waitForResponse(...)` on the actual `PUT`, replacing the
   button-state wait) is structurally immune to the same race, and passed 3/3 re-runs against a warm
   server. That is a smaller sample than the 9-run regimen the original finding is based on, and
   this Verifier neither wrote nor was consulted on the edit - it is reported as a strong candidate
   fix for the next round to formally verify, not as something this round gets to credit itself
   with closing via an unreviewed live edit.
   - `src/web/e2e/users.spec.ts:38-47` (current content), `src/web/src/app/features/identity/user-form.ts:151-157`
2. **C67's "e volta a pageNumber=1" claim had a surviving mutant as this Verifier received the tree,
   and the test was then edited mid-round by another agent (see Process note).**
   `list-query.spec.ts`'s "pesquisar envia searchTerm e volta a primeira pagina" test passed with
   the `pageNumber: 1` reset removed from `search()` in all 4 list screens, because the test never
   navigated to a later page before searching (mutation-confirmed, see Faults injected). **This
   Verifier independently re-ran the same mutation against the edited test** (which now navigates to
   page 2 first): the mutant **is killed** (`expected '2' to be '1'`). Again, a real and apparently
   correct fix, landed through a process this review cannot vouch for - the next round should verify
   it properly rather than this one crediting an edit it did not review.
   - `src/web/src/app/features/identity/users-list.ts:183`, `src/web/src/app/shared/list-query.spec.ts:107-124` (current content)
3. **`user-detail.ts`'s new self-edit visibility branch had zero test coverage as this Verifier
   received the tree, and two cases were added mid-round by another agent (see Process note).**
   `canEdit()` lets a user see "Editar" on their own profile without `identity.user.manage`,
   mirroring the API's `UserManageOrSelf` policy; mutating the branch (`||` -> `&&`) failed nothing
   in the full 121-test suite as received. **This Verifier ran the two new cases** added to
   `user-detail.spec.ts` mid-round ("a propria pessoa ve Editar sem identity.user.manage", "nem
   manager nem a propria pessoa nao ve Editar") and both pass against the real `canEdit()`. Same
   caveat as #1 and #2: a plausible, well-targeted fix, arrived at through a process outside this
   review's mandate to approve.
   - `src/web/src/app/features/identity/user-detail.ts:35,80-84`, `src/web/src/app/features/identity/user-detail.spec.ts:58-91` (current content)
3a. **C68's "sem escolha do utilizador nenhum sortBy é enviado" is unproven for the reverted-after-
    choosing case, confirmed by a surviving mutant, as this Verifier received the tree** - and a
    fourth mid-round edit (beyond the three above) appeared while this report was being finalized.
    No test clicked a sortable header a third time (MatSort's asc->desc->none cycle); the guard
    suppressing `sortBy` on that reversion could be deleted with every existing test staying green,
    on all 4 screens. A third `header!.click()` plus assertions on the reverted `sortBy`/
    `sortDirection` being absent were then added to `list-query.spec.ts`; the mutation, re-applied
    against that edited test, **is killed**. Same caveat as #1-#3: a real fix, outside this review's
    mandate to credit.
    - `src/web/src/app/features/identity/users-list.ts:189-190`, `src/web/src/app/shared/list-query.spec.ts:151-160` (current content)
4. **`plan.md`'s shell-nav claim is stale: 6 items ship, not 5.** The sibling AI-agents feature added
   a 6th item (`data-testid="nav-agents"`, "Agentes") to the same `shell.ts` this feature's plan
   fixes at "Utilizadores, Roles, Permissões, Tenants, AI" (5), within this feature's own commit
   range. The pre-existing coverage gap (achado 1) persists unchanged, but its true member count is
   now 6 in both `plan.md`'s Observable row and `checks.md`'s Coverage row.
   - `src/web/src/app/shell/shell.ts:28-62`, `.specs/features/web-frontend/plan.md:161`
5. **The Test-policy Evidence list doesn't classify the new search/sort or self-edit decision
   logic.** `search()`/`changeSort()` (4 list screens) and `user-detail.ts`'s `canEdit()` are real
   branch points with no row in `checks.md`'s Test-policy section naming them - which is how both
   ended up under-proven in the first place.
   - `.specs/features/web-frontend/checks.md:283-304`
6. **Seven checks still name two behaviours and prove one** - C8, C13, C15, C19, C20, C21, C54, all
   unchanged since round 2, all *user-deferred* (achado 1, 3). Listed so the count is not lost.
   - `src/web/src/app/core/http/refresh-coordinator.ts:51`, `src/web/src/app/features/identity/login.ts:139,145`,
     `src/web/src/app/features/identity/users-list.ts:220-227`, `src/web/src/app/shell/shell.ts:52-59`
7. **Two `Test policy` rows remain unmet** (`LOCAL_403` no case; two entry-point error paths none) -
   unchanged since round 2. *User-deferred (achado 5).*
   - `src/web/src/app/core/http/api.interceptor.ts:29`
8. **`POST /api/v1/identity/logout` still has no Surface/Coverage row/check here** - unchanged.
   *User-deferred (achado 2).*
   - `src/web/src/app/shell/shell.ts:109`

**Closed this round on its own merits (not via the mid-round edits above), listed so the record is
complete:** the ordering/sort **contract** contradiction (round-2 gap 1 - the plan's false claim
that the API exposes no sort - closed by the real search+sort implementation and by C67/C68 existing
at all, independent of C68's own remaining precision gap above); the C1/AC1 contradiction (gap 2);
the Swept data-lifecycle row (gap 7); the `GetRole` route consolidation (`STATE.md` achado 9,
mutation-confirmed); the route-coverage guard's method-awareness (`STATE.md` achado 8,
mutation-confirmed).

## Gate

`ng test --no-watch` **121 passed / 0 failed** (23 files, current tree); `tsc --noEmit` exit 0 on
both project references; `dotnet test tests/ArchitectureTests --filter TemplateConfigTests` 2
passed; Playwright C60/C65 passed on every run; **C66 failed 1 of 9 runs** against the version this
round began with. Faults: 6 injected, 3 killed, **3 survived**, against that same starting version.

The verdict is FAIL on the merits of the tree this Verifier was asked to check: three surviving
mutants and a non-deterministic e2e proof, on top of round 2's seven still-unresolved, user-deferred
check failures. Separately and at least as importantly: **this round's own process broke down** -
three files under review were edited live by an unauthorized agent while being verified, and the
shared report file was overwritten by multiple agents in parallel. That is disclosed in full above
and should be fixed at the orchestration level before a round 4 is dispatched.
