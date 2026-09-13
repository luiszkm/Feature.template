# Auth cookie contract verification

**Verdict**: FAIL
**Profile**: ui
**Diff range**: 902d206..d92feea (round-2 fix: `d92feea`)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier; a different agent from round 1)

Eighteen of nineteen checks are proven with located evidence at `d92feea`. The fix closes every
round-1 FAIL: `SolutionFileTests` now exists and both its tests run and pass (C17, C18), the
cookieless logout is asserted at the HTTP boundary (C6), the `Secure` attribute is read off a real
`Set-Cookie` header (C7), and logout's `429` is produced by the real limiter instead of being
credited to an MSW stub. All five injected mutants died, four of them on assertion surfaces the fix
itself created. The one check that does not hold is the new **C19**: its first half is proven and
mutation-killed, its second half ("o contrato declara esse status nas três rotas com a política
`auth`") is asserted by no cited proof and its premise is contradicted by the code - four routes
carry that policy, not three, and the fourth (`POST /api/v1/identity/register`) declares no `429`
in the contract that this feature made authoritative.

**Scope.** Round-2 scope is `d92feea`'s diff plus every round-1 verdict that was not PASS (C6, C17,
C18) plus the round-1 findings the fix claims to close. Each section below states what was
re-verified at `d92feea` and what is carried from round 1. Every proof was re-run in full at
`d92feea` regardless of scope. Two files in `d92feea` belong to the *other* feature and were not
verified here: `src/web/src/app/shared/screens.spec.ts` (new, covers `web-frontend`'s `Forbidden`
and `NotFound` screens) and `.specs/features/web-frontend/{plan,checks}.md`.

## Binding sources

Re-opened only where the fix touched the interface (`src/Api/openapi.json`, the three endpoint
registrations); the rest is carried from round 1.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| `src/Api/openapi.json` - the contract this feature makes authoritative | yes - **re-read at `d92feea`**, 30 operations, all `/api/v1/**`; login `200,400,401,409,429`, refresh `200,401,404,409,429`, logout `204,429` | none with the plan's `Surface` - round 1's contradiction ("`Surface` names `429`, the document declares it on none") is **closed** by `d92feea`. Residual, user-deferred: the drift guards still compare paths and methods only (`OpenApiContractTests.cs:53-58`), so a status drift passes both directions | `POST /api/v1/identity/register` carries the same `auth` policy (`src/Api/Features/Identity/RegisterUser.cs:69`) and declares `201,400,409` - no `429`. The fix declared `429` on three of the four routes on that policy |
| `src/Api/Features/Identity/Login.cs`, `RefreshAccessToken.cs`, `Logout.cs` (endpoint registrations) | yes - re-read at `d92feea`; each gained `.ProducesProblem(StatusCodes.Status429TooManyRequests)` (`Login.cs:113`, `RefreshAccessToken.cs:128`, `Logout.cs:57`) and nothing else changed | none - the handlers and the cookie writes are untouched by the fix | - |
| plan `Sources` - "conversa" | no - not a retrievable artifact; nothing exists to open (carried from round 1) | - | - |
| `.specs/features/web-frontend/plan.md` - door 3, door 5 | carried from round 1 - `Landing` and `Observable` read in full; round 1's `Uncovered` cell ("confirm cancelado ⇒ no `POST /identity/logout`" has no check) is **closed** at `d92feea` by C11's second proof, `shell.spec.ts:55-72` | none - door 3 is explicitly superseded by this plan's `Landing` | - |
| `.specs/features/auth-cookie-contract/plan.md` (`Surface`, `Relations`, `Landing`, `Observable`) | carried from round 1 | none | `document docs/security/RBAC_MATRIX.md` is an `Observable` row with no check - user-deferred (`.specs/STATE.md`, achado 9) |
| `src/Api/Features/Identity/RefreshCookie.cs` | carried from round 1 - `:19-26` decides the attributes; `:22` `Secure = context.Request.IsHttps` | none | - |

**Screen enumeration (`ui`).** Carried from round 1 - the fix touches no interface file. `shell.ts`
is byte-identical at `d92feea` (the diff adds a test, not a branch), so the composition round 1
enumerated stands: no new screen, control, indicator or region; the only selector-reachable change
remains `Sair`. Round 1's one enumeration gap was the cancelled-confirmation branch, and it now has
a check (C11, second proof). The `RBAC_MATRIX.md` row stays uncovered by user decision.

## Checks

Every proof re-run at `d92feea` in four batched invocations (`dotnet test` × 3, `ng test` × 6 -
one per front proof because the *command itself* was under verification - plus one Playwright run).
Each named test appears individually in the output. `Prov.` says whether the row's verdict was
re-judged this round or carried from round 1; every `Evidence` line number was re-located at
`d92feea` with `rg`, including the carried ones.

| Check | Claim | Proof run | Evidence | Result | Prov. |
| --- | --- | --- | --- | --- | --- |
| C1 | login emits `Set-Cookie pt_refresh` with `httponly`, `samesite=strict`, `path=/api/v1/identity` | `dotnet test tests/E2ETests --filter "…Login_ShouldSetHttpOnlyRefreshCookie"` - Aprovado | `tests/E2ETests/Identity/IdentityAuthE2ETests.cs:72-76` - `Assert.Contains("pt_refresh=", cookie)` · `Assert.Contains("httponly", …)` · `Assert.Contains("samesite=strict", …)` · `Assert.Contains("path=/api/v1/identity", …)` | PASS | carried r1, proof re-run + citation re-located at `d92feea` |
| C2 | login body carries no `refreshToken` | same batch - Aprovado | `IdentityAuthE2ETests.cs:89-90` - `Assert.False(body.RootElement.TryGetProperty("refreshToken", out _))` | PASS | carried r1, re-run at `d92feea` |
| C3 | refresh reads the cookie and returns a different `pt_refresh` | same batch - Aprovado | `IdentityAuthE2ETests.cs:104,106` - `Assert.Equal(HttpStatusCode.OK, refresh.StatusCode)` · `Assert.NotEqual(firstCookie, secondCookie)` | PASS | carried r1, re-run at `d92feea` |
| C4 | refresh without the cookie returns `401` | same batch - Aprovado | `IdentityAuthE2ETests.cs:117` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS | carried r1, re-run at `d92feea` |
| C5 | logout with cookie returns `204`, clears the cookie, and the old token then fails `401` | same batch - Aprovado | `IdentityAuthE2ETests.cs:131` - `Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode)`; `:132-134` - `Assert.Contains(logout.Headers.GetValues("Set-Cookie"), header => header.StartsWith("pt_refresh=;", …))`; `:138` - `Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode)` | PASS | carried r1, re-run at `d92feea` |
| C6 | logout without cookie returns `204` and changes no record | `dotnet test tests/E2ETests --filter "…Logout_WithoutCookie_ShouldReturn204_AndTouchNothing"` - Aprovado; `dotnet test tests/Api.Tests --filter "…LogoutTests.Handle_ShouldNoOp_WhenTokenIsMissing"` - Aprovado | **new boundary proof** `IdentityAuthE2ETests.cs:153` - `Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode)` on a cookieless `POST /api/v1/identity/logout`; `:158` - `Assert.Equal(HttpStatusCode.OK, refresh.StatusCode)` replaying the untouched cookie, which is the "changed no record" half; own level `tests/Api.Tests/Identity/LogoutTests.cs:24` - `Assert.False(revoked)` | PASS | **re-judged** - round-1 FAIL (level gap) closed |
| C7 | the cookie carries `Secure` over HTTPS and not over plain HTTP | `dotnet test tests/E2ETests --filter "…Login_CookieShouldNotBeSecure_OverPlainHttp"` - Aprovado; `dotnet test tests/Api.Tests --filter "…RefreshCookieTests.Options_ShouldSetSecure_ByScheme"` - Aprovado ×2 (`isHttps: True`, `isHttps: False`) | **new boundary proof** `IdentityAuthE2ETests.cs:172` - `Assert.DoesNotContain("secure", cookie, OrdinalIgnoreCase)` over a real `Set-Cookie`; own level `tests/Api.Tests/Identity/LogoutTests.cs:62` - `Assert.Equal(isHttps, options.Secure)` with `[InlineData(true)]`/`[InlineData(false)]` at `:53-54`. Residual: only the plain-HTTP half crosses the wire; the HTTPS half is still `CookieOptions` only | PASS | **re-judged** - round-1 level gap closed |
| C8 | every front request to `/api/v1/**` carries `withCredentials` | `npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "envia withCredentials"` - `✓ apiInterceptor > envia withCredentials`, `Tests 1 passed, 8 skipped`, exit 0 | `src/web/src/app/core/http/api.interceptor.spec.ts:60` - `expect(decorated.withCredentials).toBe(true)` | PASS | **re-judged** - proof command rewritten and now runnable |
| C9 | after login `localStorage['pt.auth']` holds `tenantKey` and `user` and no token field | `… --include src/app/features/identity/login.spec.ts --filter "guarda a sessao e navega para users"` - `✓`, `1 passed, 3 skipped` | `src/web/src/app/features/identity/login.spec.ts:40` - `expect(Object.keys(stored).sort()).toEqual(['tenantKey', 'user'])`; `:43` - `expect(raw.toLowerCase()).not.toContain('token')` | PASS | **re-judged** - command rewritten |
| C10 | the front's refresh is emitted with no token body and the session survives | `… --include src/app/core/http/api.interceptor.spec.ts --filter "401 renova e repete"` - `✓`, `1 passed, 8 skipped` | `api.interceptor.spec.ts:92-94` - `expect(body.totalCount).toBe(1)` · `expect(requests.filter(r => r.url.pathname === REFRESH)).toHaveLength(1)`. Precision gap (user-deferred): "sem corpo com token" is unasserted; the empty body lives at `src/web/src/app/core/http/refresh-coordinator.ts:29` | PASS | **re-judged** - command rewritten; precision gap carried r1 |
| C11 | `Sair` calls `POST /api/v1/identity/logout` before clearing local state, and cancelling the dialog calls nothing and clears nothing | `… --include src/app/shell/shell.spec.ts --filter "logout chama a API"` - `✓`, `1 passed, 2 skipped`; `… --filter "cancelar o dialogo nao termina a sessao"` - `✓`, `1 passed, 2 skipped` | `src/web/src/app/shell/shell.spec.ts:48-50` - `expect(requests.filter(r => r.url.pathname === '/api/v1/identity/logout')).toHaveLength(1)`; **new branch** `:69-71` - `expect(requests).toHaveLength(0)` · `expect(localStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull()` · `expect(session.user()).not.toBeNull()`. Precision gap (user-deferred): the ordering the claim names is still unasserted | PASS | **re-judged** - cancelled branch added; ordering gap carried r1 |
| C12 | against the real API, reloading renews the session with no token in `localStorage` | `PW_CHANNEL=chrome npx playwright test e2e/auth.spec.ts -g "renova o token expirado"` - 1 passed (real API on `:5080`) | `src/web/e2e/auth.spec.ts:20` - `expect(refreshCalls.length).toBeGreaterThan(0)`; `:30` - `expect(storage.local.toLowerCase()).not.toContain('token')`; `:32` - `expect(storage.cookies).not.toContain('pt_refresh')` | PASS | carried r1, re-run at `d92feea` |
| C13 | the served document equals the committed `src/Api/openapi.json` and covers the 30 `features.json` routes | `dotnet test tests/E2ETests --filter "…Document_ShouldMatch_TheCommittedContract"` - Aprovado; `dotnet test tests/ArchitectureTests --filter "…Document_ShouldBeGenerated_AtBuildTime"` - Aprovado | `tests/E2ETests/Common/OpenApiDocumentTests.cs:60-62` - `Assert.True(committed == generated, …)`; `tests/ArchitectureTests/OpenApiContractTests.cs:93-94` - `Assert.Equal(FeatureRoutes().Count, routes.Count)`. Carried note: the second test is still named after the superseded door and asserts a route count, not build-time generation | PASS | carried r1, re-run at `d92feea` |
| C14 | a `features.json` route absent from `openapi.json` fails the architecture tests | `dotnet test tests/ArchitectureTests --filter "…EveryFeatureRoute_ShouldExist_InTheDocument"` - Aprovado | `tests/ArchitectureTests/OpenApiContractTests.cs:73-75` - `Assert.True(missing.Count == 0, …)` over `FeatureRoutes().Except(DocumentedRoutes())` | PASS | carried r1, re-run at `d92feea` |
| C15 | an `/api/v1/**` route of the document absent from `features.json` fails the architecture tests | `dotnet test tests/ArchitectureTests --filter "…EveryDocumentedRoute_ShouldExist_InFeaturesJson"` - Aprovado | `OpenApiContractTests.cs:83-85` - `Assert.True(missing.Count == 0, …)` over `DocumentedRoutes().Except(FeatureRoutes())` | PASS | carried r1, re-run at `d92feea` |
| C16 | a path called by a front client and absent from `openapi.json` fails the front test run | `… --include src/app/architecture.spec.ts --filter "clientes so chamam rotas documentadas"` - `✓`, `1 passed, 5 skipped` | `src/web/src/app/architecture.spec.ts:65` - `expect(called.filter((path) => !documented.has(path))).toEqual([])`; `:64` - `expect(called.length).toBeGreaterThan(0)` guards the vacuous pass | PASS | **re-judged** - command rewritten |
| C17 | every `.csproj` in the repository is listed in `Product.Template.sln`, which is what makes `dotnet build` at the root compile instead of exiting `MSB1003` | `dotnet test tests/ArchitectureTests --filter "…SolutionFileTests.Solution_ShouldListEveryProject"` - **Aprovado** (the class now exists) | `tests/ArchitectureTests/SolutionFileTests.cs:50-52` - `Assert.True(missing.Count == 0, "Projects missing from Product.Template.sln: " + …)` over every `*.csproj` found under the repo root minus `bin`/`obj`; `:45` - `Assert.NotEmpty(projects)` guards the vacuous pass. Supporting: `dotnet build Product.Template.sln` at `d92feea` - `0 Erro(s)`, four projects | PASS | **re-judged** - round-1 FAIL (no test existed) closed |
| C18 | the solution's project GUIDs are the ones `.template.config/template.json` regenerates | `dotnet test tests/ArchitectureTests --filter "…SolutionFileTests.ProjectGuids_ShouldMatch_TemplateConfig"` - **Aprovado** | `tests/ArchitectureTests/SolutionFileTests.cs:77` - `Assert.Equal(templateGuids, solutionGuids)`, both sides sorted ordinal: `:61-66` reads `template.json` `guids`, `:68-75` regexes `Project(…) = …, "{GUID}"` out of the solution. Non-vacuous: `template.json` `guids` holds 4 entries (read directly), and `Product.Template.sln:5,7,9,11` holds the same 4 | PASS | **re-judged** - round-1 FAIL (no test existed) closed |
| C19 | the 21st call to `POST /api/v1/identity/logout` inside the window returns `429`, **and the contract declares that status on the three routes with the `auth` policy** | `dotnet test tests/E2ETests --filter "…Logout_ShouldReturn429_WhenTheAuthLimiterTrips"` - Aprovado | First half proven: `IdentityAuthE2ETests.cs:190` - `Assert.Equal(HttpStatusCode.TooManyRequests, last)`. Second half: **no assertion anywhere names `429` in the document**; the nearest evidence is `OpenApiDocumentTests.cs:60-62`'s whole-document equality, which pins the declaration but is not cited by this check and asserts nothing about `429`. Two further defects: (a) the premise is false - four routes carry `SecurityConfiguration.AuthRateLimitPolicy` (`Login.cs:109`, `RefreshAccessToken.cs:124`, `Logout.cs:55`, `RegisterUser.cs:69`), and the fourth declares no `429`; (b) the loop at `:184-188` breaks on the first `429`, so the assertion holds if *any* of the 21 calls is rejected - the ordinal the claim names is never observed | FAIL | **new check, verified at `d92feea`** |

## Coverage

Recomputed from the authority over each set, not read back from the author's table. The rows whose
authority `d92feea` touched (the three route-status rows, the new `429` row, the doors row) were
recomputed from scratch at `d92feea`; the remaining rows are carried from round 1 and their
authority is unchanged by the diff.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/v1/identity/login` statuses (5) | **recomputed at `d92feea`** - `src/Api/openapi.json` `paths./api/v1/identity/login.post.responses` = `200,400,401,409,429` | 200 C1/C2 (`IdentityAuthE2ETests.cs:71,89`) · 400 web-frontend C3 · 401 web-frontend C2 · 409 web-frontend C15 · 429 C19 by shared limiter - `AddFixedWindowLimiter` registers **one unpartitioned limiter per policy** (`src/Api/Host/Configurations/SecurityConfiguration.cs:136-141`), and `Login.cs:109` attaches that policy, so C19's `Assert.Equal(TooManyRequests, last)` is an assertion about the same limiter object. No proof issues a 21st `POST /login` | - |
| `POST /api/v1/identity/refresh` statuses (5) | **recomputed at `d92feea`** - `openapi.json` = `200,401,404,409,429` | 200 C3 (`:104`) · 401 C4 (`:117`) · 404 web-frontend C14 · 409 web-frontend C15 · 429 C19, same shared limiter (`RefreshAccessToken.cs:124`) | - |
| `POST /api/v1/identity/logout` statuses (2) | **recomputed at `d92feea`** - `openapi.json` = `204,429`; `Logout.cs:48` `Results.NoContent()` on both branches | `204` with cookie C5 (`:131`) · `204` without cookie C6 (`:153`) · `429` C19 (`:190`), tripped against the real limiter | - |
| routes carrying the `auth` rate-limit policy (4) | **recomputed at `d92feea`** from the code, which is the authority over which routes the policy is attached to: `Login.cs:109`, `RefreshAccessToken.cs:124`, `Logout.cs:55`, `RegisterUser.cs:69` | login - declared `429`, C19 by shared limiter · refresh - declared `429`, C19 · logout - declared `429`, C19 at the boundary · register - **declares no `429`** (`openapi.json` `…/register.post.responses` = `201,400,409`) and no proof | `POST /api/v1/identity/register` - the fourth route on the same limiter. C19's claim counts three; `checks.md:91` gives the set a row sized 3. The contract now says this route cannot return `429` while the code says it can, and the drift guards compare paths and methods only, so nothing catches it |
| cookie attributes (5) | `src/Api/Features/Identity/RefreshCookie.cs:19-26` and the wire | `HttpOnly` C1 (`:73`) · `SameSite=Strict` C1 (`:74`) · `Path` C1 (`:75`) · `Max-Age` C1 (`:76`, presence only; the value is asserted one level down at `LogoutTests.cs:66` `Assert.Equal(TimeSpan.FromDays(30), options.MaxAge)`) · `Secure` **now at the boundary** C7 (`IdentityAuthE2ETests.cs:172`, absent over http) plus `LogoutTests.cs:62` for both rows | `Max-Age` value on the wire - user-deferred precision gap (`.specs/STATE.md`, achado 8) |
| one-way doors in the plan's `Landing` (6 rows, 5 live) | `.specs/features/auth-cookie-contract/plan.md` `Landing` | refresh em cookie C1 · corpo sem token C2 (`:89`) · rota `logout` C5 (`:131`) · documento **versionado** C13 (`OpenApiDocumentTests.cs:60`) · solução na raiz **C17 (`SolutionFileTests.cs:50`) + C18 (`:77`)** · documento gerado no build - superseded, correctly absent from the table now (`src/Api/Api.csproj` carries no `Microsoft.Extensions.ApiDescription.Server`) | - |
| `features.json` ↔ `openapi.json` routes (30) | `features.json` - 30 slices; `src/Api/openapi.json` - 30 operations, all `/api/v1/**` | all 30 in both directions - C14 (`OpenApiContractTests.cs:73`) and C15 (`:83`) are set differences, not samples | - |
| contract drift directions (2) | `tests/ArchitectureTests/OpenApiContractTests.cs` | features.json → documento C14 (`:73`) · documento → features.json C15 (`:83`) | - |
| front surfaces touched (3) | the files this change edits under `src/web/src/app` | interceptor C8 (`api.interceptor.spec.ts:60`) · sessão C9 (`login.spec.ts:40`) · shell C11, now both branches (`shell.spec.ts:49`, `:69`) | - |
| bootstrap: HTTP providers (3 assemblies) | each assembly file opened directly (carried from round 1) | `src/web/src/app/app.config.ts:10` · `src/web/src/test-providers.ts:20` - both `provideHttpClient(withInterceptors([apiInterceptor]))` · Playwright assembly = the real app (`src/web/playwright.config.ts:21` `command: 'npm run start'`), exercised live by C12 | - |

## Test policy rows

Both rows round 1 recorded as unmet were re-judged at `d92feea`; the other three are carried from
round 1 and the fix classifies no new file.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, atravessado por uma fronteira | `src/Api/Features/Identity/RefreshCookie.cs` | boundary C1/C7 · own level C7 | **yes** (re-judged - was `no` in round 1). Boundary: `IdentityAuthE2ETests.cs:172` `Assert.DoesNotContain("secure", cookie, OrdinalIgnoreCase)` now reads the attribute off a real header, alongside C1's `:73-76`. Own level: `LogoutTests.cs:62` with `[InlineData(true)]`/`[InlineData(false)]` covers both rows of the `Secure` decision table |
| Decide, atravessado por uma fronteira | `src/Api/Features/Identity/Logout.cs` | boundary C5/C6 · own level C6 | **yes** (re-judged - was `no` in round 1). Both branches now cross the boundary: revoke `IdentityAuthE2ETests.cs:131,138`, no-cookie `:153,158`. Own level for both: `LogoutTests.cs:24` `Assert.False(revoked)` and `:42` `Assert.True(revoked)` |
| Decide, não atravessado por uma fronteira | none - the `Evidence` block classifies no file here | - | n/a - no input, nothing to judge (carried r1) |
| Ponto de entrada que não decide | `src/Api/Features/Identity/Login.cs`, `RefreshAccessToken.cs` | one at the boundary | yes (re-judged - the fix touched both files, metadata only: `.ProducesProblem(429)`). Accepted entry C1/C2 (`IdentityAuthE2ETests.cs:71,89`), rejected entry C4 (`:117`), rotation C3 (`:104-106`); the added metadata is pinned by C13's document equality (`OpenApiDocumentTests.cs:60`) |
| Instrumentação, pass-through | `src/web/src/app/core/http/api.interceptor.ts` | none of its own | yes - covered by consumer proofs C8 (`api.interceptor.spec.ts:60`) and C12 (`e2e/auth.spec.ts:20`) (carried r1) |

## Faults injected

Isolation was a real `git worktree add /tmp/r2b-scratch d92feea` (the tree is committed this round,
unlike round 1). Real-tree `git status --porcelain` before: ` M .specs/STATE.md`, ` M
.specs/features/web-frontend/plan.md`, ` M .specs/features/web-frontend/verification.md`, `??
.specs/LESSONS.md`, `?? .specs/lessons.json`. After removing the worktree: byte-identical to that
baseline. Nothing was stashed and nothing was checked out in the real tree; each mutation was
reverted with `git -C /tmp/r2b-scratch checkout --` inside the scratch, whose final
`git status --porcelain` was empty.

All five faults land on assertion surfaces `d92feea` **created** - one per new test, chosen so each
fault validates one of the round-1 findings the fix claims to close. Round 1 had already killed
mutants on the cookie attributes, the revoked-cookie replay, the refresh `401`, `withCredentials`
and the session store, so none of those surfaces was repeated.

| Mutation | Location | Proof run | Killed |
| --- | --- | --- | --- |
| cookieless logout returns `401` instead of `204` (early `Results.StatusCode(401)` when `RefreshCookie.Read` is null) | `src/Api/Features/Identity/Logout.cs:45-48` | C6 `Logout_WithoutCookie_ShouldReturn204_AndTouchNothing` | yes - `Assert.Equal() Failure: Expected: NoContent, Actual: Unauthorized` at `IdentityAuthE2ETests.cs:153` |
| `Secure = context.Request.IsHttps` -> `Secure = true` | `src/Api/Features/Identity/RefreshCookie.cs:22` | C7 `Login_CookieShouldNotBeSecure_OverPlainHttp` | yes - `Assert.DoesNotContain() Failure: Sub-string found … "h=/api/v1/identity; secure; samesite=stri"` at `:172` |
| `.RequireRateLimiting(SecurityConfiguration.AuthRateLimitPolicy)` removed from the logout endpoint | `src/Api/Features/Identity/Logout.cs:55` | C19 `Logout_ShouldReturn429_WhenTheAuthLimiterTrips` | yes - `Assert.Equal() Failure: Expected: TooManyRequests, Actual: NoContent` at `:190` |
| project path in the solution goes stale (`tests\E2ETests\E2ETests.csproj` -> `…\E2E.csproj`) | `Product.Template.sln:11` | C17 `SolutionFileTests.Solution_ShouldListEveryProject` (run together with C18's test: 1 failed, 1 passed) | yes - `Projects missing from Product.Template.sln: tests\E2ETests\E2ETests.csproj` at `SolutionFileTests.cs:50` |
| `Sair` ignores the confirmation result (`if (!confirmed)` -> `if (false)`) | `src/web/src/app/shell/shell.ts:96` | C11 `cancelar o dialogo nao termina a sessao` | yes - `AssertionError: expected [ { method: 'POST', …(3) } ] to have a length of +0 but got 1` at `shell.spec.ts:69` |

Not mutated, at the five-fault cap: C18's `ProjectGuids_ShouldMatch_TemplateConfig`. Its
non-vacuity was settled by reading instead - `template.json` `guids` has four entries and the
solution's four `Project(…)` lines carry the same four, so `Assert.Equal(templateGuids,
solutionGuids)` compares two non-empty sets. The F4 run exercised it as the passing half of the
same invocation.

**One experiment deliberately not run, because the source settles it.** C19's loop is
`for (var attempt = 0; attempt < 21 && last != HttpStatusCode.TooManyRequests; attempt++)` followed
by `Assert.Equal(TooManyRequests, last)` (`IdentityAuthE2ETests.cs:184-190`). The assertion holds
iff at least one of the first 21 calls was rejected; it cannot distinguish the 21st from the 1st.
A `PermitLimit = 20 -> 1` mutation at `SecurityConfiguration.cs:138` therefore survives by
construction. Recorded as a finding rather than spending a sixth fault.

## Walkthrough with the user

**Not run** - no channel to the user is available in this execution, unchanged from round 1. Step 5
is not recorded as passed. The user-facing surface the fix adds is one test, not one interaction,
so the human judgment still outstanding is the same one round 1 left open (`.specs/STATE.md`,
achado 10).

## Findings

### Closed by `d92feea` (round-1 findings 1-8, 10)

1. **C17 / C18 had no test** - `tests/ArchitectureTests/SolutionFileTests.cs` now exists; both
   tests run and pass at `d92feea`, the listing test is guarded against a vacuous pass
   (`:45 Assert.NotEmpty`), and a stale project path was made to fail it.
2. **C6 level gap** - closed by `Logout_WithoutCookie_ShouldReturn204_AndTouchNothing`; both halves
   of the claim are now asserted at the boundary (`:153`, `:158`) and the mutant died.
3. **C7 level gap** - closed by `Login_CookieShouldNotBeSecure_OverPlainHttp` (`:172`); the
   `RefreshCookie.cs` `Test policy` row is now met.
4. **Five unrunnable front proof commands** - all six front proofs (C8, C9, C10, C11 ×2, C16) run
   as written and each selects **exactly one** test. Verified with a negative control: the same
   command with a bogus `--filter` reports `Test Files 1 skipped | Tests 3 skipped` and exits `0`,
   so "1 passed" is the load-bearing part of each result, and it is present in all six.
5. **Logout's `429` miscredited to an MSW stub** - now proven against the real limiter (C19 first
   half), and killed by removing `.RequireRateLimiting`.
6. **Coverage credited a superseded door** - the "documento no build" member is gone; the row
   declares 5 live doors and C18 joins C17 on the solution door.
7. **`Swept` dependency-failure row** - now says the document is generated by the in-memory test
   host, which is what `OpenApiDocumentTests.cs:39-50` does.
8. **Cancelled-confirm branch of `Sair` had no check** - C11 gained a second proof
   (`shell.spec.ts:55-72`), and the mutant that ignores the confirmation died.

### Surviving / new (ranked)

1. **C19's second clause is unproven and its premise is false.** "o contrato declara esse status
   nas três rotas com a política `auth`" (`checks.md:76`): four routes carry that policy
   (`Login.cs:109`, `RefreshAccessToken.cs:124`, `Logout.cs:55`, `RegisterUser.cs:69`), and the
   fourth declares `201,400,409` with no `429`. No assertion in the repository names `429` in the
   document; the declaration is pinned only indirectly, by `OpenApiDocumentTests.cs:60-62`'s
   whole-document equality, which this check does not cite. The contract now states that
   `POST /api/v1/identity/register` cannot return `429` while the shared limiter says it can, and
   the drift guards compare paths and methods only, so nothing catches it.
2. **C19's test cannot see the ordinal its claim names.** `IdentityAuthE2ETests.cs:184-190` breaks
   the loop on the first `429` and asserts only that the last observed status is `429`; a
   `PermitLimit` of 1 would pass it. Either assert the first 20 calls are `204` and the 21st is
   `429`, or drop "A 21ª chamada" from the claim.
3. **C7's HTTPS half still has no boundary proof.** The new test proves `secure` is absent over
   http; the `Set-Cookie` carrying `secure` over https is asserted only against `CookieOptions`
   (`LogoutTests.cs:62`). The `Test policy` row is met (it demands one boundary proof), so this is
   a residual, not a failure.
4. **C13's second proof is still named after the rejected door.**
   `OpenApiContractTests.Document_ShouldBeGenerated_AtBuildTime:89` asserts a route count; nothing
   generates the document at build time. Carried from round 1, unchanged by the fix.

### Deferred by the user - carried from round 1, not re-litigated

From `.specs/STATE.md`: the contract guards compare paths and methods but not statuses (achado 7);
the three precision gaps - C1's `Max-Age` value on the wire, C10's "refresh sem corpo", C11's
"revogar antes de limpar" ordering (achado 8); and `docs/security/RBAC_MATRIX.md` as an
`Observable` row with no check (achado 9). Step 5 remains un-run for want of a user channel
(achado 10).

## Gate

`python3 scripts/validate_verification.py auth-cookie-contract --root /Users/luissoares/Repos/Feature.template`
- exit 1, 1 error (verdict is FAIL), 0 warnings.

Proof runs behind this report, all at `d92feea`:

- `dotnet build Product.Template.sln` - `0 Erro(s)`, four projects, exit 0
- `dotnet test tests/E2ETests --no-build --filter "…IdentityAuthE2ETests|…OpenApiDocumentTests.Document_ShouldMatch_TheCommittedContract"` - Total 11, Aprovados 11
- `dotnet test tests/Api.Tests --no-build --filter "…LogoutTests|…RefreshCookieTests"` - Total 4, Aprovados 4
- `dotnet test tests/ArchitectureTests --no-build --filter "…OpenApiContractTests|…SolutionFileTests"` - Total 5, Aprovados 5 (round 1: "Nenhum teste corresponde ao filtro")
- `npx ng test --no-watch --include <spec> --filter "<name>"` ×6 as written in `checks.md`, plus one
  negative control - 6 × `1 passed`, control `0 passed / 3 skipped`
- `PW_CHANNEL=chrome npx playwright test e2e/auth.spec.ts -g "renova o token expirado"` - 1 passed,
  against the real API on `:5080`
