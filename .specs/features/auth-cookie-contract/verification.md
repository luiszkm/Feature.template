# Auth cookie contract verification

**Verdict**: PASS
**Profile**: ui
**Diff range**: d92feea..7148c0c (round-3 re-verification; the closures below ride an unrelated
commit `51f706f` and its follow-up `abb8a68`, not a dedicated fix for this feature)
**Round**: 3 - scoped
**Verifier**: independent sub-agent (author != verifier; a different agent from rounds 1 and 2)

Round 2 failed on one check, **C19**, for two reasons: its second clause named a false premise
("três rotas" carry the `auth` policy - there are four) with no assertion anywhere naming `429` in
the document, and its first clause's loop broke on the first `429`, so the test could not
distinguish the 21st call from the 1st. Both are closed. `RegisterUser.cs` now declares `429`
(`:70-72`) and `openapi.json`'s `register` path lists it; a new architecture guard,
`OpenApiContractTests.EveryRateLimitedRoute_ShouldDeclare_TooManyRequests`, scans every file using
`AuthRateLimitPolicy` and fails if the route's declared responses omit `429` - `checks.md` itself
was updated to say "quatro, incluindo `register`". `IdentityAuthE2ETests.Logout_ShouldReturn429_WhenTheAuthLimiterTrips`
now records all statuses and asserts the first N succeed and the N+1th is `429` (an `Assert.All`
over the below-bound calls, then an indexed `Assert.Equal` on the trip). Both mutations killed both
proofs (below).

Neither fix landed as a dedicated commit for this feature: `51f706f` ("feat(users): implement user
editing...") bundled the `checks.md`/register/guard changes alongside unrelated user-editing work,
and `abb8a68` then raised the Testing-environment rate limit from 20 to 200/min so an unrelated
AI-agents Playwright suite could log in once per spec, updating the same test's loop bound and
counts in the same motion. That side effect leaves two new, non-failing findings this round: the
literal numbers in `checks.md`'s own prose for C19 ("as 20 primeiras... e a 21ª") and C13 ("cobre
as 30 rotas") are now stale against the code they cite (the Testing limiter is 200/201; the route
count is 38) - see Findings. The underlying guards are count- and boundary-agnostic and are
unaffected; only the human-readable claim text drifted.

Two files in `d92feea..HEAD` belong to *other* verified-separately features and were not
re-verified here: the AI-agents module (`src/Api/Features/Ai/**`, `src/web/.../features/ai/**`,
`.specs/features/agentes/**`) and `web-frontend`'s own spec files. I confirmed neither touches this
feature's surface (see Binding sources).

## Binding sources

Re-opened where the fix's diff or an unrelated commit touched the interface; the rest is carried
from round 2 (itself carried from round 1 where round 2 said so).

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| `src/Api/openapi.json` - the contract this feature makes authoritative | yes - re-read at `7148c0c`; login `200,400,401,409,429`, refresh `200,401,404,409,429`, logout `204,429`, **register now `201,400,409,429`** (matches the plan's `Surface` table exactly; round 2's contradiction, register missing `429`, is closed) | none | - |
| `src/Api/Features/Identity/RegisterUser.cs` | yes - `:68` `.RequireRateLimiting(RateLimitPolicies.AuthRateLimitPolicy)`, `:72` `.ProducesProblem(StatusCodes.Status429TooManyRequests)` | none | - |
| `src/Api/Features/Identity/Login.cs`, `RefreshAccessToken.cs`, `Logout.cs` | yes - re-read at `7148c0c`; functionally unchanged since `d92feea` (only the `56fe7e3` refactor moved `SecurityConfiguration.AuthRateLimitPolicy` to `RateLimitPolicies.AuthRateLimitPolicy` in `src/Api/Shared/SecurityPolicies.cs:22`, a rename, same value `"auth"`) | none | - |
| `src/Api/Features/Identity/RefreshCookie.cs` | yes - re-read; `Secure = context.Request.IsHttps` now at `:19` (was `:22` - unrelated `using` removal), `OptionsFor` at `:14-24`. The `56fe7e3` refactor changed `Write` (`:26-33`) from reading `IOptions<JwtSettings>` directly to `IJwtTokenService.GetRefreshTokenExpirationDays()`, which still resolves to `_settings.RefreshTokenExpirationDays` (`src/Api/Host/Security/JwtTokenService.cs:63`) - a pure abstraction, same value | none | - |
| `features.json` / `src/Api/openapi.json` route-count parity | recomputed at `7148c0c` - both now hold **38** `/api/v1/**` operations (was 30 at `d92feea`; AI-agents and user-editing features added routes in between). `EveryFeatureRoute_ShouldExist_InTheDocument` / `EveryDocumentedRoute_ShouldExist_InFeaturesJson` are set differences, not counts, so the growth does not weaken them - only `checks.md`'s own C13 prose ("cobre as 30 rotas") is now a stale literal number, see Findings | none | n/a |
| `.specs/features/auth-cookie-contract/plan.md` (`Surface`, `Relations`, `Landing`, `Observable`) | re-opened - **byte-identical diff to `d92feea`** (`git diff d92feea..HEAD -- plan.md` is empty). Confirmed `docs/security/RBAC_MATRIX.md`'s Identity section (`:31-33`, logout already listed) is untouched by this round's diff - only the unrelated AI-agents section grew. The `document docs/security/RBAC_MATRIX.md` `Observable` row with no check is user-deferred (`.specs/STATE.md`, achado 9), see Findings | none | n/a |
| `src/web/src/app/shell/shell.ts`, `shell.spec.ts` | re-opened - **not** byte-identical this round; both gained an AI-agents nav item (`nav-agents`, gated on `ai.available()`) and a matching spec (`esconde AI e Agentes quando a flag esta off`) | none for this feature - the added surface is entirely the AI module's `ai.available()` gate, not `Sair`/logout; the `Sair` composition (confirm dialog, two branches) is unchanged | - |
| AI-agents surface (`src/Api/Features/Ai/**`) for overlap with this feature | checked - `grep -rn "AuthRateLimitPolicy"` across `src/Api/Features` and `src/Api/Shared` finds exactly the four Identity routes; no AI-agents route joins the `auth` rate-limit policy, and `git log d92feea..HEAD -- src/Api/Features/Identity` shows only `51f706f`/`56fe7e3`/`0138072`/`fb5deb6`, none of which are AI-agents commits | none | - |

**Screen enumeration (`ui`).** `shell.ts` is no longer byte-identical (see row above), but the only
change is the AI module's own nav item; the `Sair` button, its confirm dialog and its two branches
(confirmed logout / cancelled) are unchanged in markup and behaviour. No new enumeration gap for
this feature.

## Checks

Every proof re-run in full at `7148c0c` in four batched `dotnet test` invocations plus six `ng
test` invocations plus one Playwright run - each named test confirmed individually via
`--logger "console;verbosity=detailed"` (backend) or the `Tests 1 passed | N skipped` line (front).
The C19 rate-limit test was run three times total (once in the full-filter batch, twice more
standalone) with no flakiness.

| Check | Claim | Proof run | Evidence | Result | Prov. |
| --- | --- | --- | --- | --- | --- |
| C1 | login emits `Set-Cookie pt_refresh` with `httponly`, `samesite=strict`, `path=/api/v1/identity` | `dotnet test tests/E2ETests --filter "…IdentityAuthE2ETests or …OpenApiDocumentTests.Document_ShouldMatch_TheCommittedContract"` - 11 Aprovado | `tests/E2ETests/Identity/IdentityAuthE2ETests.cs:72-76` - `Assert.Contains("pt_refresh=", cookie)` · `"httponly"` · `"samesite=strict"` · `"path=/api/v1/identity"` | PASS | carried r2, re-run at `7148c0c` (file diff confirmed to touch only the C19 test body) |
| C2 | login body carries no `refreshToken` | same batch | `:89-90` - `Assert.False(body.RootElement.TryGetProperty("refreshToken", out _))` | PASS | carried r2, re-run at `7148c0c` |
| C3 | refresh reads the cookie and returns a different `pt_refresh` | same batch | `:104,106` - `Assert.Equal(HttpStatusCode.OK, refresh.StatusCode)` · `Assert.NotEqual(firstCookie, secondCookie)` | PASS | carried r2, re-run at `7148c0c` |
| C4 | refresh without the cookie returns `401` | same batch | `:117` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS | carried r2, re-run at `7148c0c` |
| C5 | logout with cookie returns `204`, clears the cookie, old token then fails `401` | same batch | `:131` `Assert.Equal(NoContent, logout.StatusCode)`; `:132-134` `Assert.Contains(..., header => header.StartsWith("pt_refresh=;"))`; `:138` `Assert.Equal(Unauthorized, refresh.StatusCode)` | PASS | carried r2, re-run at `7148c0c` |
| C6 | logout without cookie returns `204` and changes no record | same batch; `dotnet test tests/Api.Tests --filter "…LogoutTests.Handle_ShouldNoOp_WhenTokenIsMissing"` - 4 Aprovado | `:153` `Assert.Equal(NoContent, logout.StatusCode)`; `:158` `Assert.Equal(OK, refresh.StatusCode)` replaying the untouched cookie; own level `tests/Api.Tests/Identity/LogoutTests.cs:24` `Assert.False(revoked)` | PASS | carried r2, re-run at `7148c0c` |
| C7 | cookie carries `Secure` over HTTPS, not over plain HTTP | same batches (`Login_CookieShouldNotBeSecure_OverPlainHttp`; `RefreshCookieTests.Options_ShouldSetSecure_ByScheme` ×2) | `:172` `Assert.DoesNotContain("secure", cookie, OrdinalIgnoreCase)` over a real header; own level `LogoutTests.cs:62` `Assert.Equal(isHttps, options.Secure)`, `[InlineData(true/false)]` at `:53-54`. Residual carried: only the plain-HTTP half crosses the wire | PASS | carried r2, re-run at `7148c0c`; re-judged - `RefreshCookie.cs`'s `IJwtTokenService` refactor does not touch the `Secure` decision |
| C8 | every front request to `/api/v1/**` carries `withCredentials` | `npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "envia withCredentials"` - `1 passed, 8 skipped` | `src/web/src/app/core/http/api.interceptor.spec.ts:60` `expect(decorated.withCredentials).toBe(true)` | PASS | carried r2, re-run at `7148c0c` (file byte-identical since `d92feea`) |
| C9 | after login `localStorage['pt.auth']` holds `tenantKey`+`user`, no token field | `… --include .../login.spec.ts --filter "guarda a sessao e navega para users"` - `1 passed, 3 skipped` | `login.spec.ts:40` `expect(Object.keys(stored).sort()).toEqual(['tenantKey','user'])`; `:43` `not.toContain('token')` | PASS | carried r2, re-run at `7148c0c` |
| C10 | front's refresh emitted with no token body, session survives | `… --include api.interceptor.spec.ts --filter "401 renova e repete"` - `1 passed, 8 skipped` | `api.interceptor.spec.ts:92-94`. Precision gap (user-deferred, achado 8): empty body itself lives at `src/web/src/app/core/http/refresh-coordinator.ts:29`, unasserted | PASS | carried r2, re-run at `7148c0c` |
| C11 | `Sair` calls logout before clearing state; cancel calls/clears nothing | `… --include shell.spec.ts --filter "logout chama a API"` - `1 passed, 3 skipped`; `--filter "cancelar o dialogo nao termina a sessao"` - `1 passed, 3 skipped` | `src/web/src/app/shell/shell.spec.ts:50-52` `expect(requests.filter(...)).toHaveLength(1)`; `:71-73` `expect(requests).toHaveLength(0)` · `.not.toBeNull()` ×2. Precision gap (user-deferred): ordering still unasserted | PASS | **citations refreshed** - file gained the AI-agents nav test above these two, shifting both by +2 lines (`:48-50`→`:50-52`, `:69-71`→`:71-73`); assertions themselves unchanged |
| C12 | against the real API, reload renews the session with no token in storage | `PW_CHANNEL=chrome npx playwright test e2e/auth.spec.ts -g "renova o token expirado"` - 1 passed | `src/web/e2e/auth.spec.ts:42` `expect(refreshCalls.length).toBeGreaterThan(0)`; `:52` `not.toContain('token')`; `:54` `not.toContain('pt_refresh')` | PASS | **citations refreshed** - two unrelated Playwright tests were prepended to the file, shifting this test's assertions from `:20/30/32` to `:42/52/54`; test body itself unchanged |
| C13 | served document equals committed `openapi.json`, covers the 30 (**now 38**) `features.json` routes | `dotnet test tests/E2ETests --filter "…OpenApiDocumentTests.Document_ShouldMatch_TheCommittedContract"` - Aprovado; `dotnet test tests/ArchitectureTests --filter "…OpenApiContractTests.Document_ShouldCover_EveryFeatureRoute"` - Aprovado | `tests/E2ETests/Common/OpenApiDocumentTests.cs:60-61` `Assert.True(committed == generated, ...)`; `tests/ArchitectureTests/OpenApiContractTests.cs:95-96` `Assert.Equal(FeatureRoutes().Count, routes.Count)` · `Assert.Contains("POST /api/v1/identity/login", routes)`. Round-2 finding #4 **closed**: the test was renamed from `Document_ShouldBeGenerated_AtBuildTime` to `Document_ShouldCover_EveryFeatureRoute`, matching what it actually asserts (a count, not build-time generation) | PASS | **re-judged** - name now honest; residual: `checks.md`'s "30 rotas" is a stale literal (actual 38) - see Findings; the assertion itself is count-agnostic |
| C14 | a `features.json` route absent from `openapi.json` fails architecture tests | `dotnet test tests/ArchitectureTests --filter "…OpenApiContractTests.EveryFeatureRoute_ShouldExist_InTheDocument"` - Aprovado | `OpenApiContractTests.cs:73` `FeatureRoutes().Except(DocumentedRoutes())`; `:75-77` `Assert.True(missing.Count == 0, ...)` | PASS | **citation refreshed** (+2 lines from round 2's `:73-75`, same file gained the new guard test after it) |
| C15 | an `/api/v1/**` route absent from `features.json` fails architecture tests | `dotnet test tests/ArchitectureTests --filter "…OpenApiContractTests.EveryDocumentedRoute_ShouldExist_InFeaturesJson"` - Aprovado | `OpenApiContractTests.cs:83` `DocumentedRoutes().Except(FeatureRoutes())`; `:85-87` `Assert.True(missing.Count == 0, ...)` | PASS | **citation refreshed** (+2 lines) |
| C16 | a front-called path absent from `openapi.json` fails `npm test` | `… --include architecture.spec.ts --filter "clientes so chamam rotas documentadas"` - `1 passed, 5 skipped` | `src/web/src/app/architecture.spec.ts:71` `expect(called.filter((p) => !documented.has(p))).toEqual([])`; `:70` `expect(called.length).toBeGreaterThan(0)` guards vacuity | PASS | **citation refreshed** - an uncommitted, unrelated edit to a *different* `it()` block earlier in the same file (making the `features.json`↔client-calls guard method-aware, per the working tree's own comment about a `GetRole` regression) shifted this test from `:64-65` to `:70-71`; this test's own body is untouched |
| C17 | every `.csproj` is listed in `Product.Template.sln` | `dotnet test tests/ArchitectureTests --filter "…SolutionFileTests.Solution_ShouldListEveryProject"` - Aprovado | `tests/ArchitectureTests/SolutionFileTests.cs:45` `Assert.NotEmpty(projects)`; `:50-52` `Assert.True(missing.Count == 0, ...)`. Supporting: `dotnet build Product.Template.sln` at `7148c0c` - `0 Erro(s)`, four projects | PASS | carried r2, re-run at `7148c0c` (file byte-identical since `d92feea`) |
| C18 | solution's project GUIDs match `.template.config/template.json` | `dotnet test tests/ArchitectureTests --filter "…SolutionFileTests.ProjectGuids_ShouldMatch_TemplateConfig"` - Aprovado | `SolutionFileTests.cs:61-66` reads `template.json` `guids`; `:68-75` regexes `Project(...) = ..., "{GUID}"`; `:77` `Assert.Equal(templateGuids, solutionGuids)` | PASS | carried r2, re-run at `7148c0c` |
| C19 | the 20 first calls to logout return `204`, the 21st (**now the 200 first / 201st**) returns `429`; the contract declares `429` on every `auth`-policy route (**four, including `register`**) | `dotnet test tests/E2ETests --filter "…IdentityAuthE2ETests.Logout_ShouldReturn429_WhenTheAuthLimiterTrips"` - Aprovado (×3 runs, no flake); `dotnet test tests/ArchitectureTests --filter "…OpenApiContractTests.EveryRateLimitedRoute_ShouldDeclare_TooManyRequests"` - Aprovado | First half: `IdentityAuthE2ETests.cs:192` `Assert.All(statuses.Take(200), s => Assert.Equal(NoContent, s))`; `:193` `Assert.Equal(TooManyRequests, statuses[200])` - now the exact ordinal is asserted, mutation-killed at both ends (below). Second half: `OpenApiContractTests.cs:105-140` scans every `Features/**/*.cs` containing `"AuthRateLimitPolicy"` for its `Map*` route, and fails if any lacks `429` in `openapi.json`; `:122` `Assert.NotEmpty(rateLimited)` guards vacuity; confirmed by direct read that exactly 4 files match (`Login.cs`, `RefreshAccessToken.cs`, `Logout.cs`, `RegisterUser.cs`) and all 4 declare `429` | PASS | **re-judged** - both round-2 defects closed. Residual: `checks.md:76`'s literal "as 20 primeiras... e a 21ª" is stale against the code (Testing/Development limiter is 200, per `SecurityConfiguration.cs:140` `? 200 : 20`, raised from 20 by `abb8a68` for an unrelated AI-agents e2e need); the test correctly tracks whatever the real limiter is, so the assertion is not wrong, only the checks.md prose is |

## Coverage

Recomputed from the authority over each set. Rows whose authority this round's diff touched (the
route-status sets, the `429`-declaration set, the route-count parity set) were recomputed from
scratch at `7148c0c`; the rest is carried from round 2, its authority unchanged.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/v1/identity/login` statuses (5) | `src/Api/openapi.json` = `200,400,401,409,429` (unchanged) | 200 C1/C2 · 400/401/409 web-frontend · 429 C19 | - |
| `POST /api/v1/identity/refresh` statuses (5) | `openapi.json` = `200,401,404,409,429` (unchanged) | 200 C3 · 401 C4 · 404/409 web-frontend · 429 C19 | - |
| `POST /api/v1/identity/logout` statuses (2) | `openapi.json` = `204,429` (unchanged) | 204 C5/C6 · 429 C19 (boundary) | - |
| routes carrying the `auth` rate-limit policy (**4**) | **recomputed at `7148c0c`** from the code: `Login.cs:107`, `RefreshAccessToken.cs:122`, `Logout.cs:54`, `RegisterUser.cs:68` all reference `RateLimitPolicies.AuthRateLimitPolicy` | login/refresh/logout - declared `429`, C19 by shared limiter and boundary; **register - now declared `429`** (`openapi.json` `.../register.post.responses` = `201,400,409,429`), guarded by `EveryRateLimitedRoute_ShouldDeclare_TooManyRequests` (round 2's gap is closed; fault-injected by removing register's `429` and the guard caught it) | - |
| `features.json` ↔ `openapi.json` routes (**38**, was 30) | **recomputed at `7148c0c`** - both hold 38 `/api/v1/**` operations (grown by AI-agents and user-editing features, unrelated to this feature) | all 38 in both directions - C14/C15 are set differences, unaffected by the count (only `checks.md`'s "30 rotas" prose is stale, see Findings) | - |
| cookie attributes (5) | `RefreshCookie.cs:14-24` and the wire | `HttpOnly`/`SameSite`/`Path`/`Max-Age`(presence) C1 (`:72-76`) · `Max-Age` value one level down, `LogoutTests.cs:66` · `Secure` at the boundary C7 (`:172`) plus `LogoutTests.cs:62`. `Max-Age`'s exact value on the wire is a user-deferred precision gap (achado 8), see Findings | n/a |
| one-way doors in the plan's `Landing` (5 live) | `plan.md` `Landing` (byte-identical to `d92feea`) | refresh cookie C1 · corpo sem token C2 · rota logout C5 · documento versionado C13 · solução na raiz C17+C18 | - |
| contract drift directions (2) | `tests/ArchitectureTests/OpenApiContractTests.cs` | features.json→documento C14 (`:73,75-77`) · documento→features.json C15 (`:83,85-87`) | - |
| front surfaces touched (3) | files under `src/web/src/app` this feature edits | interceptor C8 · sessão C9 · shell C11 (both branches, citations refreshed) | - |
| bootstrap: HTTP providers (3 assemblies) | each assembly opened directly - byte-identical since `d92feea` | `app.config.ts:10` · `test-providers.ts:20` · Playwright's real app via `playwright.config.ts:21` | - |

## Test policy rows

Re-judged the rows the diff's files touched; the rest carried from round 2 (already re-judged
there, unchanged since).

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, atravessado por uma fronteira | `src/Api/Features/Identity/RefreshCookie.cs` | boundary C1/C7 · own level C7 | **yes** (re-judged) - the `56fe7e3` refactor changed only how `Write` looks up the expiration days (`IJwtTokenService` instead of `IOptions<JwtSettings>`, same underlying value); the `Secure` decision at `:19` and its proofs (`IdentityAuthE2ETests.cs:172`, `LogoutTests.cs:62`) are untouched |
| Decide, atravessado por uma fronteira | `src/Api/Features/Identity/Logout.cs` | boundary C5/C6 · own level C6 | **yes** (re-judged) - only `using` statements and the rate-limit-policy identifier changed (`:1`, `:54`); the decision at `:17-18` and its boundary/own-level proofs are unchanged |
| Ponto de entrada que não decide | `src/Api/Features/Identity/Login.cs`, `RefreshAccessToken.cs`, **`RegisterUser.cs`** (newly in scope this round - it joined the `auth` policy's declared-`429` surface) | one at the boundary | yes - `RegisterUser.cs` gained only metadata (`.ProducesProblem(429)`, `:72`) and adds nothing to the entry-point's own decisions; its accepted/rejected entries are proven by `RegisterAndLogin_ShouldSucceed` (`IdentityAuthE2ETests.cs:32`) and `web-frontend`'s own register checks, and the `429` declaration itself is now proven by `EveryRateLimitedRoute_ShouldDeclare_TooManyRequests` |
| Decide, não atravessado por uma fronteira | none | - | n/a - carried forward, still nothing classified under this row |
| Instrumentação, pass-through | `src/web/src/app/core/http/api.interceptor.ts` | none of its own | yes - covered by C8/C12 (carried, file byte-identical) |

## Faults injected

Isolation: `git worktree add <scratch> HEAD` at `7148c0c`. Real-tree `git status --porcelain`
before: the working tree's pre-existing unrelated changes (`.gitignore`, `.specs/LESSONS.md`,
`.specs/STATE.md`, `.specs/features/agentes/checks.md`, `.specs/features/web-frontend/{checks,plan}.md`,
`.specs/lessons.json`, `features.json`, `src/Api/Features/Authorization/GetRole.cs`,
`src/Api/openapi.json`, a deleted `src/Api/test-results/.last-run.json`, `src/web/e2e/users.spec.ts`,
`src/web/src/app/architecture.spec.ts`, `tests/Api.Tests/Ai/CreateAgentFileTests.cs`,
`tests/Api.Tests/Common/TestServiceFactory.cs`, and three untracked `.specs/features/**` files) -
none of which touch this feature's surface, confirmed by inspecting each diff. After removing the
worktree: `git status --porcelain` on the real tree is byte-identical to that baseline. Each
mutation was reverted inside the scratch tree before the next (fault A via `git checkout --`,
fault B superseded fault A after the revert); nothing was stashed.

Both faults target the two surfaces this round's fix actually created - the register `429`
declaration/guard, and C19's newly-observable ordinal - since those are the only genuinely new
assertion surfaces in the diff (the renamed `Document_ShouldCover_EveryFeatureRoute` test asserts
the same expression as its predecessor, and the `56fe7e3`/`0138072`/`fb5deb6` refactors are pure
renames already exercised by every proof above passing green).

| Mutation | Location | Proof run | Killed |
| --- | --- | --- | --- |
| removed `429` from `register`'s declared responses in `openapi.json` | `src/Api/openapi.json` `paths./api/v1/identity/register.post.responses` | `OpenApiContractTests.EveryRateLimitedRoute_ShouldDeclare_TooManyRequests` | yes - `Rate-limited routes not declaring 429 in openapi.json: POST /api/v1/identity/register` |
| shifted the Testing/Development `PermitLimit` by one, `200` -> `199` | `src/Api/Host/Configurations/SecurityConfiguration.cs:140` | `IdentityAuthE2ETests.Logout_ShouldReturn429_WhenTheAuthLimiterTrips` | yes - `Assert.All() Failure: 1 out of 200 items in the collection did not pass. [199]: Expected: NoContent, Actual: TooManyRequests` at `:192` |

## Walkthrough with the user

**Not run** - no channel to the user is available in this execution, same as rounds 1 and 2. No new
user-facing surface was added this round (the fix is two test/contract changes plus an unrelated
nav item belonging to the AI-agents feature). Step 5 remains un-run (`.specs/STATE.md`, achado 10).

## Findings

### Closed since `d92feea` (round-2 surviving findings 1, 2, 4)

1. **C19's false premise, closed.** `RegisterUser.cs` now carries `.ProducesProblem(429)`
   (`:72`) and `openapi.json`'s `register` path lists `429`; a new architecture guard
   (`OpenApiContractTests.cs:105-140`) makes this structural, not incidental - fault-injected by
   removing the declaration and confirmed to fail.
2. **C19's unobserved ordinal, closed.** The test now asserts `Assert.All(statuses.Take(200), ...
   NoContent)` then `Assert.Equal(TooManyRequests, statuses[200])` (`:192-193`) - a `PermitLimit`
   off-by-one no longer survives, confirmed by fault-injecting exactly that shift.
3. **C13's stale test name, closed.** Renamed `Document_ShouldBeGenerated_AtBuildTime` to
   `Document_ShouldCover_EveryFeatureRoute`, matching the assertion (a route-count equality, not
   build-time generation).

### New this round (ranked) - both non-failing prose staleness, not functional gaps

1. **`checks.md`'s C19 claim now cites the wrong numbers.** `checks.md:76` reads "As 20 primeiras
   chamadas... e a 21ª"; the code and test it cites now use 200/201, because `abb8a68` raised the
   Testing/Development `PermitLimit` from 20 to 200 (`SecurityConfiguration.cs:140`) so an
   unrelated AI-agents Playwright suite could log in once per spec. The test still correctly
   asserts *whatever* the real limiter is, and the fault above confirms it tracks the current
   value precisely - the check does not silently pass a wrong implementation. But its prose is
   now factually wrong about the codebase it describes, and would drift again the next time the
   limiter changes. Not scored against the check's `Result` because the underlying behavioural
   claim (N calls succeed, N+1th trips) is still exactly what is proven.
2. **`checks.md`'s C13 claim now cites the wrong count.** `checks.md:55` reads "cobre as 30
   rotas"; `features.json` and `openapi.json` now hold 38 `/api/v1/**` operations each (AI-agents
   and user-editing features added routes since this number was written). The proof
   (`Assert.Equal(FeatureRoutes().Count, routes.Count)`) is count-agnostic, so the drift is
   cosmetic, not a coverage gap - but it is the same failure mode as finding 1: a check that
   hardcodes a concrete cardinality will keep going stale as unrelated features land. Worth
   rewording both checks to state the invariant ("as many as `features.json` currently has") rather
   than a snapshot number, so the text stops lying on the next unrelated PR.

### Residual, re-judged and confirmed non-failing (carried from round 2)

3. **C7's HTTPS half still has no boundary proof.** `Set-Cookie` carrying `secure` over https is
   asserted only against `CookieOptions` (`LogoutTests.cs:62`), not over a real HTTPS connection.
   The `Test policy` row demands one boundary proof and one own-level proof, both present, so this
   remains a residual rather than an unmet row - re-confirmed this round since `RefreshCookie.cs`'s
   only change (the `IJwtTokenService` indirection) does not touch the `Secure` decision.

### Deferred by the user - carried, not re-litigated (`.specs/STATE.md`)

Achado 7 (general): the contract guards compare paths and methods, and now `429` for
`auth`-policy routes specifically, but not every status generally - closed only for the case that
failed. Achado 8: precision gaps in C1 (`Max-Age` wire value), C10 ("refresh sem corpo"), C11
("revogar antes de limpar" ordering) - unchanged, confirmed the cited files carry no new
assertions on these since `d92feea`. Achado 9: `docs/security/RBAC_MATRIX.md` remains an
`Observable` row with no check - the Identity section (logout already listed at `:33`) is
untouched by this round's diff; only the unrelated AI-agents section grew. Achado 10: step 5
remains un-run for want of a user channel.

## Gate

Proof runs behind this report, all at `7148c0c`:

- `dotnet build Product.Template.sln` - `0 Erro(s)`, four projects, exit 0
- `dotnet test tests/E2ETests --filter "…IdentityAuthE2ETests or …OpenApiDocumentTests.Document_ShouldMatch_TheCommittedContract"` - Total 11, Aprovados 11 (each test individually confirmed via detailed logger)
- `dotnet test tests/E2ETests --filter "…Logout_ShouldReturn429_WhenTheAuthLimiterTrips"` - re-run standalone ×2 more (3 total), all Aprovado, no flake
- `dotnet test tests/Api.Tests --filter "…LogoutTests or …RefreshCookieTests"` - Total 4, Aprovados 4
- `dotnet test tests/ArchitectureTests --filter "…OpenApiContractTests or …SolutionFileTests"` - Total 6, Aprovados 6
- `npx ng test --no-watch --include <spec> --filter "<name>"` ×6 as written in `checks.md` - 6 × `1 passed`
- `PW_CHANNEL=chrome npx playwright test e2e/auth.spec.ts -g "renova o token expirado"` - 1 passed, against the shared real API on `:5080` (already running for the concurrent `web-frontend` verification; not started or killed by this session)
- 2 fault injections in an isolated `git worktree`, both killed; worktree discarded, real tree's `git status --porcelain` confirmed byte-identical to baseline before and after

`python3 .cursor/skills/tlc-spec-lean/scripts/validate_verification.py auth-cookie-contract --root /Users/luissoares/Repos/Feature.template`
- `validate_verification: 0 error(s), 0 warning(s) across [auth-cookie-contract]`
