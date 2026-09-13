# Web front-end (Angular 22) verification

**Verdict**: FAIL
**Profile**: ui
**Diff range**: 902d206..d92feea (fix under review: `d92feea`)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier; different agent from round 1)

Scoped per `verify.md` "Re-verifying after a fix": the fix's diff plus every round-1 verdict that
was not PASS. Everything else carries forward and says so, row by row. All proofs were re-run in
full at `d92feea`.

Four of round 1's findings are closed: the surviving `forbidden` mutant, the C1/contract
contradiction at the check level, the unrunnable proof commands, and the stale door 3. **One new
finding is opened by the fix itself**: the four ordering rows were deleted from `plan.md` (and
`AD-006` written into `.specs/STATE.md`) on the stated ground that "a API não expõe sort" — the
versioned contract declares `SortBy` and `SortDirection` on all four list endpoints and the
handlers implement them, default `createdAt` desc. That is a check/plan row contradicting a
binding source, which `verify.md` ranks above a failing proof. Eight checks remain not proven;
seven of those are the user's explicitly deferred findings.

## How the proofs were run

Every front proof command in `checks.md` was rewritten by the fix from
`npx vitest run <file> -t "<name>"` (unrunnable — round 1) to
`npx ng test --no-watch --include <file> --filter "<name>"`. Verified at `d92feea`:

1. **The form runs.** Six of the rewritten commands were executed verbatim, one process each:
   C1, C10's new second proof, C18, C32, C62, plus a deliberately non-matching control. Every real
   one printed its named test as run and passed, e.g.
   `✓ web src/app/shared/screens.spec.ts > Forbidden > mostra a mensagem de sem permissao e volta atras` ·
   `Tests 1 passed | 1 skipped (2)` · exit 0.
2. **No filter matches nothing.** The control
   (`--include login.spec.ts --filter "este teste nao existe de todo"`) printed `Tests 4 skipped (4)`
   and **exited 0** — the silent-green failure mode is live on this runner. It does not fire here:
   all 64 `ng test` proof commands were matched against the full (file, suite, test) inventory
   produced by the verbose run, and **every one selects at least one real test**; 57 select exactly
   one, and the seven that select more are the intentionally table-driven ones (C18/C19/C20 → 4 rows
   each over the 4 list screens, C25 → 5 permission rows). The matcher model was validated against
   the six live runs above and agreed exactly.
3. **One invocation for the whole target.** `npx ng test --no-watch --reporters verbose` in
   `src/web`: **20 files, 85 tests, 85 passed, 0 failed**, every named test shown individually.
   (Round 1: 19 files / 82 tests; the fix adds `screens.spec.ts` with 2 and one test to
   `shell.spec.ts`.)
4. **One Playwright invocation** for C60 + C65:
   `PW_CHANNEL=chrome npx playwright test e2e/users.spec.ts e2e/auth.spec.ts -g "cria e elimina um utilizador|renova o token expirado"` — 2 passed, against the real API on `:5080`
   (in-memory DB, started at `d92feea`; a stale pre-fix API instance was found holding the port and
   was replaced before the run).
5. **One dotnet invocation** for C63:
   `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~TemplateConfigTests"` — 2 passed.
6. C62's second half re-run by the verifier: `npx tsc -p tsconfig.app.json --noEmit` exit 0,
   `npx tsc -p tsconfig.spec.json --noEmit` exit 0.

Claim-by-claim on the fix's own description: (1) forbidden mutant — **closed**, independently
re-killed below; (2) C1 rewritten — **closed at the check, open in the plan**; (3) proof commands —
**closed**; (4) ordering — **not closed, and the replacement text is false**; (5) door 3 superseded —
**closed**; (6) 65 checks — **closed** (65 IDs, C1..C65, none missing, none duplicated).

## Binding sources

`verified at d92feea` for `openapi.json` (changed by the fix) and for the `forbidden` / `shell`
screens (the only interface surfaces whose coverage the fix touched). All other rows
`carried from round 1`.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| `src/Api/openapi.json` (30 operations, 18 paths) — *verified at d92feea; the fix edited this file* | yes - all 30 operations re-parsed, `AuthTokenResponse` and every list operation's `parameters` block read | **(a) The four list GETs declare `SortBy` and `SortDirection`** (`/api/v1/identity/users`, `/api/v1/authorization/roles`, `/api/v1/authorization/permissions`, `/api/v1/tenants`), implemented at `src/Api/Features/Identity/User.cs:101`, `src/Api/Features/Authorization/Role.cs:129`, `src/Api/Features/Authorization/Permission.cs:84`, `src/Api/Features/Tenants/Tenant.cs:103`, each defaulting to `createdAt` desc. `plan.md:43` now says "nenhum endpoint aceita sort", `plan.md:166,175,180,184` say "a API não expõe sort", `.specs/STATE.md:15` (AD-006) says the same. All four statements are false against the source `.specs/STATE.md:12` (AD-003) names as the authority. **(b) `AuthTokenResponse` has no `refreshToken`** and `src/Api/Features/Identity/Login.cs` writes it to the `pt_refresh` cookie; `plan.md:53` (AC 1), `plan.md:206` (Flow 4) and `plan.md:273` (Impact) still say the front stores `refreshToken` in `localStorage`. C1 was rewritten to the shipped shape but still cites AC 1, which states the opposite | `POST /api/v1/identity/logout` — **the fix added a `429` to it**, so the uncovered set grew from 1 status to 2. Still no `Surface` row, no `Coverage` row and no check in this feature. Proven in `.specs/features/auth-cookie-contract` (C5, C6, C19) — *deferred by the user (`STATE.md` achado 2)* |
| `features.json` (30 slices / 30 routes) | *carried from round 1* | plan `plan.md:6` says "29 rotas"; the index has 30 — *deferred by the user (achado 6)* | `Logout` slice has no check in this feature — *deferred (achado 2)* |
| `docs/security/RBAC_MATRIX.md` (11 policies) | *carried from round 1* | none | - |
| `docs/architecture/vsa.md` · `.cursor/rules/architecture-vsa.mdc` | *carried from round 1* | none - enforced by C59 | - |
| plan `Observable` + `src/web/src/app/app.routes.ts` (14 screens) — *`forbidden` and `shell` re-verified at d92feea; the other 12 carried from round 1* | yes - `forbidden` and `shell` re-enumerated against `screens.ts` and `shell.ts` | `plan.md:219` omits `/tenants/new` and the `''`→`users` redirect; `plan.md:169` says "skeleton" where `user-form.ts:36` renders a `mat-progress-bar` — *deferred (achado 6)* | **5 element groups, down from round 1's 11 - enumerated below** |

### Elements the design decides that no check reaches — re-enumerated at `d92feea`

Round 1 listed 11 (13 table rows). Two are now closed by the fix, four were deleted from the plan
(and are the subject of contradiction (a) above). The rest are the user's deferred `achado 1`.

| Screen | Element the plan decides | Where it lives | Check | Status |
| --- | --- | --- | --- | --- |
| `forbidden` | message "Sem permissão para esta operação" + button back to the previous route (`plan.md:190`) | `screens.ts:33-36` | **C10, second proof** | **CLOSED at d92feea** - `screens.spec.ts:12,18` |
| `shell` | Sair asks for confirmation in the shared dialog (`plan.md:160`) | `shell.ts:90-98` | none *in this feature*; asserted by `shell.spec.ts:69-71`, named by `auth-cookie-contract` C11 | **assertion CLOSED at d92feea**; this feature's `checks.md` still names no check for it |
| `shell` | navigation order and membership: fixed Utilizadores, Roles, Permissões, Tenants, AI (`plan.md:161`) | `shell.ts:30-54`, five `data-testid="nav-*"` | none | open - *carried from round 1, deferred (achado 1)* |
| `shell` | nav items without permission are not rendered (`plan.md:159`) | `shell.ts:32,39,46` | only C25, on a synthetic `Host` (`permission.directive.spec.ts:11`), never on the shell | open - *carried, deferred* |
| `login` | submit disabled while the request runs (`plan.md:153`) | `login.ts:54` `[disabled]="pending()"` | none | open - *carried, deferred* |
| `tenants-list` | deactivate confirmation says it is reversible by editing (`plan.md:183`) | `tenants-list.ts:148` | none - re-grepped at `d92feea`, source only | open - *carried, deferred* |
| `ai-chat` | empty state reads "Faça uma pergunta" (`plan.md:186`) | `chat.ts:37` | C55 asserts only that `chat-empty` exists | open - *carried, deferred* |
| list screens ×4 | empty-state labels "Nenhum utilizador"/"Nenhum role"/"Nenhuma permissão"/"Nenhum tenant" | `users-list.ts:71`, `roles-list.ts:164`, `permissions-list.ts:70`, `tenants-list.ts:50` | C19 asserts only that `list-empty` exists | open - *carried, deferred* |
| document `src/web/AGENTS.md` | documents the layer-folder rule (`plan.md:197`) | `src/web/AGENTS.md` | none | open - *carried, deferred* |
| 4 list screens | default ordering per screen (`plan.md:166,175,180,184`) | deleted from the plan by `d92feea` | none | **new finding** - the deletion's stated rationale contradicts the contract (see (a)) |

## Checks

65 check IDs (C1..C65, none missing, none duplicated). `checks.md:8` now reads "65 checks" —
round 1's off-by-one is **closed**.

Verdict rule (unchanged from round 1): PASS when every behaviour, element, status or label the
check names has a located assertion; FAIL when one of them has no assertion anywhere in the tree.

Provenance: `refreshed at d92feea` marks rows whose citation was re-read in a file the fix touched
or whose verdict changed this round. Every other row is `carried from round 1` — its file is
byte-identical at `d92feea` (`git show --stat d92feea` touches only `screens.spec.ts` and
`shell.spec.ts` under `src/web/`), and its proof was re-run green in the batch above.

| Check | Claim | Proof run | Evidence | Result | Provenance |
| --- | --- | --- | --- | --- | --- |
| C1 | login stores `tenantKey`+`user` in `localStorage['pt.auth']`, keeps both tokens out of storage, navigates to `/users` | `ng test --include login.spec.ts --filter "guarda a sessao e navega para users"` exit 0 | `src/web/src/app/features/identity/login.spec.ts:40` - `expect(Object.keys(stored).sort()).toEqual(['tenantKey','user'])`; `:43` - `expect(raw.toLowerCase()).not.toContain('token')`; `:45` - `expect(router.url).toBe('/users')` | PASS - note: "fora de qualquer storage" is asserted only against `pt.auth`; `sessionStorage`/cookies are covered by C65, not here | **refreshed at d92feea** - claim rewritten; see Binding sources (b): it still cites AC 1, which says the opposite |
| C2 | `401` keeps `/login`, shows `detail`, preserves email | same file | `login.spec.ts:60` - `expect(text(fixture,'login-message')).toBe('Invalid email or password.')`; `:63-64` email `.value).toBe('admin@producttemplate.com')` | PASS | carried from round 1 |
| C3 | `400` maps `errors[field]` under each field | `--include problem-details.spec.ts` | `src/web/src/app/shared/problem-details.spec.ts:65-66` - `expect(text(fixture,'email-error')).toBe("'Email' is not a valid email address.")` | PASS | carried from round 1 |
| C4 | every `/api/v1/**` request carries `X-Tenant` | `--include api.interceptor.spec.ts` | `src/web/src/app/core/http/api.interceptor.spec.ts:42` - `expect(requests.at(-1)?.headers.get('X-Tenant')).toBe('acme')` | PASS | carried from round 1 |
| C5 | `Authorization: Bearer <accessToken>` | same file | `api.interceptor.spec.ts:71` - `expect(requests.at(-1)?.headers.get('Authorization')).toBe(\`Bearer ${session.accessToken()}\`)` | PASS | carried from round 1 |
| C6 | `401` → exactly one refresh + retry; refresh `401` clears + `/login` | same file | `api.interceptor.spec.ts:93-94` - refresh count `1` / users count `2`; `:106-108` - `expect(router.url).toContain('/login')`, `expect(session.accessToken()).toBeNull()` | PASS | carried from round 1 |
| C7 | three parallel `401` → one refresh | same file | `api.interceptor.spec.ts:160` - `expect(requests.filter(r=>r.url.pathname===REFRESH)).toHaveLength(1)` | PASS - "as três repetidas com o mesmo token" is not asserted | carried from round 1 |
| C8 | protected route → `/login?redirectTo=<rota>`, then back after login | `--include guards.spec.ts` | `src/web/src/app/core/guards/guards.spec.ts:13` - `expect(router.url).toBe('/login?redirectTo=%2Ftenants')` | **FAIL** - the second clause is performed by the test itself (`guards.spec.ts:16-17`); `login.ts:139-140`, the code that honours it, has no assertion | carried from round 1 - *deferred by the user (achado 3)* |
| C9 | `hasPermission` reads the `permission` claim | `--include session.store.spec.ts` | `src/web/src/app/core/session/session.store.spec.ts:14-15` - `toBe(true)` / `toBe(false)` | PASS | carried from round 1 |
| C10 | `403` renders `forbidden` with "Sem permissão para esta operação" and a back button | `--include api.interceptor.spec.ts --filter "403 abre o ecra forbidden"` **and** `--include screens.spec.ts --filter "mostra a mensagem de sem permissao"` - both exit 0 | `api.interceptor.spec.ts:169` - `expect(router.url).toBe('/forbidden')`; `src/web/src/app/shared/screens.spec.ts:12` - `expect(text(fixture,'forbidden')).toContain('Sem permissão para esta operação')`; `:18` - `expect(back).toHaveBeenCalledTimes(1)` after clicking `forbidden-back` | **PASS** | **refreshed at d92feea** - round 1 FAIL + surviving mutant, now closed; mutants F1/F2 below kill both halves |
| C11 | logout clears `pt.auth`, empties the in-memory token, navigates to `/login` | `--include session.store.spec.ts` | `session.store.spec.ts:50` - `expect(session.accessToken()).toBeNull()`; `:52` - `expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull()`; navigation at `src/web/src/app/shell/shell.spec.ts:52` - `expect(TestBed.inject(Router).url).toBe('/login')` | PASS - the navigation clause is settled by a test the check does not name | **refreshed at d92feea** (`shell.spec.ts` touched; line 52 unchanged) |
| C12 | top bar shows `firstName` and `tenantKey` | `--include shell.spec.ts` | `shell.spec.ts:29-30` - `expect(text(fixture,'session-user')).toBe('System')` / `('session-tenant')).toBe('dev')` | PASS | **refreshed at d92feea** (`shell.spec.ts` touched; lines unchanged) |
| C13 | `429` shows the rate-limit message and keeps the fields filled | `--include login.spec.ts` | `login.spec.ts:74` - `expect(text(fixture,'login-message')).toBe(RATE_LIMIT_MESSAGE)` | **FAIL** - "mantém os campos preenchidos" has no assertion and is contradicted by `login.ts:145`, which clears the password on every error | carried from round 1 - *deferred (achado 3)* |
| C14 | refresh `404` clears the session and navigates to `/login` | `--include api.interceptor.spec.ts` | `api.interceptor.spec.ts:120-121` | PASS | carried from round 1 |
| C15 | login **or refresh** `409` shows "Tenant inválido" and stores no session | `--include login.spec.ts` | `login.spec.ts:92-93` - `expect(text(fixture,'tenant-error')).toBe(INVALID_TENANT_MESSAGE)`, `expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull()` | **FAIL** - only the login case is exercised; the refresh-`409` path (`refresh-coordinator.ts:51`) has no assertion, and the Coverage table maps refresh `409` here | carried from round 1 - *deferred (achado 3)* |
| C16 | refresh `400` clears the session and navigates to `/login` | `--include api.interceptor.spec.ts` | `api.interceptor.spec.ts:133-134` | PASS | carried from round 1 |
| C17 | `/users` issues `?pageNumber=1&pageSize=20`, four columns | `--include users-list.spec.ts` | `src/web/src/app/features/identity/users-list.spec.ts:48-49`; `:54` - `expect(headers).toEqual(['Email','Nome','Criado em','Último login',''])` | PASS | carried from round 1 |
| C18 | loading shows `mat-progress-bar`, paginator disabled, 4 screens | `--include list-state.spec.ts --filter "estado de carregamento"` - 4 rows, all named in the output | `src/web/src/app/shared/list-state.spec.ts:67-70`; `list-state.ts:15` binds `list-loading` to `<mat-progress-bar>` | PASS | carried from round 1 |
| C19 | `totalCount === 0` shows the empty state **with the create action**, 4 screens | `--filter "estado vazio"` - 4 rows | `list-state.spec.ts:88` - `expect(maybeEl(fixture,'list-empty')).not.toBeNull()` | **FAIL** - the create action (`list-state.ts:20`, `users-list.ts:74`) and the per-screen label have no assertion | carried from round 1 - *deferred (achado 1)* |
| C20 | `500` **or connection error** shows the `title` and "Tentar de novo" re-runs the query, 4 screens | `--filter "estado de erro repete a query"` - 4 rows | `list-state.spec.ts:107` - `expect(text(fixture,'list-error-title')).toBe('Unexpected error')`; `:112` - `expect(calls).toBe(2)` | **FAIL** - only the `500` case is exercised; the connection-error case has no assertion | carried from round 1 - *deferred (achado 3, achado 5)* |
| C21 | paging/search **writes** `pageNumber`, `pageSize`, `searchTerm` to the URL and reloads from them | `--include users-list.spec.ts` | `users-list.spec.ts:68-70` - fed from a pre-set route stub | **FAIL** - proves only the read direction; `users-list.ts:197-205` (`router.navigate` with `queryParams`) has no assertion | carried from round 1 - *deferred (achado 3)* |
| C22 | create → `POST register`, `201` → `/users` + snackbar | `--include user-form.spec.ts` | `src/web/src/app/features/identity/user-form.spec.ts:50-51` | PASS | carried from round 1 |
| C23 | confirm delete → `DELETE`, `204` → row gone | `--include users-list.spec.ts` | `users-list.spec.ts:88` - `expect(maybeEl(fixture,'row-user-1')).toBeNull()` | PASS | carried from round 1 |
| C24 | cancel emits no HTTP request | same file | `users-list.spec.ts:105` - `expect(requests.length).toBe(before)` | PASS | carried from round 1 |
| C25 | management actions hidden without the permission and without `Admin`, 5 cases | `--include permission.directive.spec.ts` - 5 rows | `src/web/src/app/core/session/permission.directive.spec.ts:33`, `:38` | PASS | carried from round 1 |
| C26 | `/users/{id}` issues both GETs and shows both | `--include user-detail.spec.ts` | `src/web/src/app/features/identity/user-detail.spec.ts:37-38` | PASS | carried from round 1 |
| C27 | save → `PUT`, `200` → fields show the response | `--include user-form.spec.ts` | `user-form.spec.ts:94-95` | PASS | carried from round 1 |
| C28 | `404` on a detail GET renders not-found with the `title` | `--include problem-details.spec.ts` | `problem-details.spec.ts:77`, `:83` - `expect(text(fixture,'not-found-title')).toBe('Not found')` | PASS | carried from round 1 |
| C29 | register `409` marks the email field and keeps the form filled | `--include user-form.spec.ts` | `user-form.spec.ts:72-73` | PASS | carried from round 1 |
| C30 | `404` on PUT/DELETE → "Registo não encontrado" + list reload | `--include problem-details.spec.ts` | `problem-details.spec.ts:91-92` | PASS - the snackbar rendering is not asserted | carried from round 1 |
| C31 | `403` on users roles → "Sem acesso aos roles", rest of the screen intact | `--include user-detail.spec.ts` | `user-detail.spec.ts:54-56` | PASS | carried from round 1 |
| C32 | `/roles` issues `?pageNumber=1&pageSize=20`, name+description per row | `--include roles-list.spec.ts --filter "carrega a primeira pagina"` - re-run standalone, 1 test, exit 0 | `src/web/src/app/features/authorization/roles-list.spec.ts:45-46` | PASS - unlike C17 the query params are not asserted (see Test policy row 4) | carried from round 1 |
| C33 | `/roles/{id}` shows name and description | `--include role-detail.spec.ts` | `src/web/src/app/features/authorization/role-detail.spec.ts:47-48` | PASS | carried from round 1 |
| C34 | detail lists assigned permissions by `name` | same file | `role-detail.spec.ts:55` | PASS | carried from round 1 |
| C35 | assign → `POST`, `204` → appears without a new read | same file | `role-detail.spec.ts:79-80` | PASS | carried from round 1 |
| C36 | revoke → `DELETE`, `204` → leaves the list | same file | `role-detail.spec.ts:97-98` | PASS | carried from round 1 |
| C37 | create role → `POST`, `201` → row added, dialog closes | `--include roles-list.spec.ts` | `roles-list.spec.ts:69-70` | PASS | carried from round 1 |
| C38 | role `409` keeps the dialog open with `detail` at Nome | same file | `roles-list.spec.ts:94-95` | PASS | carried from round 1 |
| C39 | save role → `PUT`, `200` → row updated | same file | `roles-list.spec.ts:115` | PASS | carried from round 1 |
| C40 | delete role → `DELETE`, `204` → row gone | same file | `roles-list.spec.ts:129` | PASS | carried from round 1 |
| C41 | assign/revoke user role → POST/DELETE, each followed by a GET | `--include user-roles.spec.ts` | `src/web/src/app/features/authorization/user-roles.spec.ts:53-55`, `:61-63` | PASS | carried from round 1 |
| C42 | `/permissions` issues the GET, name+description per row | `--include permissions-list.spec.ts` | `src/web/src/app/features/authorization/permissions-list.spec.ts:45-46` | PASS | carried from round 1 |
| C43 | create permission → `POST`, `201` → appears | same file | `permissions-list.spec.ts:68` | PASS | carried from round 1 |
| C44 | permission `409` keeps the dialog open with `detail` at Nome | same file | `permissions-list.spec.ts:92-95` | PASS | carried from round 1 |
| C45 | save permission → `PUT`, `200` → row updated | same file | `permissions-list.spec.ts:114` | PASS | carried from round 1 |
| C46 | delete permission → `DELETE`, `204` → row gone | same file | `permissions-list.spec.ts:128` | PASS | carried from round 1 |
| C47 | `/tenants` issues `?pageNumber=1&pageSize=20`, four columns | `--include tenants-list.spec.ts` | `src/web/src/app/features/tenants/tenants-list.spec.ts:51-55` | PASS - `pageNumber=1` is not asserted | carried from round 1 |
| C48 | create tenant → `POST`, `201` → `/tenants` | `--include tenant-form.spec.ts` | `src/web/src/app/features/tenants/tenant-form.spec.ts:44` | PASS | carried from round 1 |
| C49 | tenant `409` keeps the form filled, `detail` at Chave | same file | `tenant-form.spec.ts:66-67` | PASS | carried from round 1 |
| C50 | save tenant → `PUT`, `200` → fields show the response | same file | `tenant-form.spec.ts:85` | PASS | carried from round 1 |
| C51 | tenant `PUT` `400` maps `errors[field]` under each field | same file | `tenant-form.spec.ts:106` | PASS | carried from round 1 |
| C52 | deactivate → `DELETE`, `204` → row shows inactive | `--include tenants-list.spec.ts` | `tenants-list.spec.ts:69` - `expect(text(fixture,'active-tenant-1')).toBe('Não')` | PASS | carried from round 1 |
| C53 | `/tenants/{id}` shows the seven `TenantOutput` fields | `--include tenant-form.spec.ts` | `tenant-form.spec.ts:117-123` - seven `expect(text(fixture,'field-*'))`, matching the seven required properties in `openapi.json` | PASS - the plan writes `{id}` where the contract says `{tenantId}` (deferred drift) | carried from round 1 |
| C54 | chat `404` "Feature disabled" **removes the AI item from the navigation** | `--include chat.spec.ts` | `src/web/src/app/features/ai/chat.spec.ts:91-92` - `expect(TestBed.inject(AiAvailability).available()).toBe(false)` | **FAIL** - level gap: the claim is about the navigation, the assertion sits on the signal; `shell.ts:52-54` has no assertion | carried from round 1 - *deferred (achado 3)* |
| C55 | send appends to history, `POST` with `message`+`history`, renders `reply` | same file | `chat.spec.ts:43-44` | PASS | carried from round 1 |
| C56 | pending disables Send and shows the typing indicator | same file | `chat.spec.ts:67-68` | PASS | carried from round 1 |
| C57 | chat `401` goes through the refresh path before any error | same file | `chat.spec.ts:118-120` | PASS | carried from round 1 |
| C58 | a `features.json` route with no client fails `npm test` | `--include architecture.spec.ts` | `src/web/src/app/architecture.spec.ts:46` - `expect(missing).toEqual([])` over all 30 entries | PASS | carried from round 1 |
| C59 | a layer folder under `features/` fails `npm test` | same file | `architecture.spec.ts:81` - `expect(walk(featuresRoot)).toEqual([])` | PASS | carried from round 1 |
| C60 | `npm run e2e` logs in, lists, creates and deletes against `:5080` with tenant `dev` | Playwright, 1 invocation, 2 passed | `src/web/e2e/users.spec.ts:20` - `await expect(row).toHaveCount(1)`; `:25` - `toHaveCount(0)`; tenant from `e2e/fixtures.ts:5,9` | PASS | re-run at d92feea against an API started at d92feea |
| C61 | `web-e2e` publishes `playwright-report` with `if: always()` | `--include architecture.spec.ts` | `architecture.spec.ts:96-98` | PASS | carried from round 1 |
| C62 | `tsconfig.json` declares `strict: true` and `tsc --noEmit` exits 0 | `--include architecture.spec.ts` + both `tsc --noEmit` runs, exit 0 | `architecture.spec.ts:89` - `expect(tsconfig.compilerOptions.strict).toBe(true)`; `src/web/tsconfig.json:13` | PASS - the named proof settles only the first half | carried from round 1, `tsc` re-run at d92feea |
| C63 | `template.json` excludes `**/node_modules/**` and `**/dist/**` | `dotnet test … TemplateConfigTests` - 2 passed | `tests/ArchitectureTests/TemplateConfigTests.cs:42` - `Assert.Contains(glob, excludes)`, once per glob (`:26-27`) | PASS | re-run at d92feea (the fix added `SolutionFileTests.cs` to the same project; the filter is unaffected) |
| C64 | HTTP providers registered in all three assemblies | `--include architecture.spec.ts` | `architecture.spec.ts:107`; assemblies read directly at `src/web/src/app/app.config.ts:10` and `src/web/src/test-providers.ts:24` | PASS - the "third assembly" is a proxy assertion (`:111`); `main.ts:4` bootstraps the same `appConfig`, so there are two | carried from round 1 |
| C65 | a session with no in-memory access token renews from the refresh cookie without returning to login | Playwright, same invocation as C60 | `src/web/e2e/auth.spec.ts:18-20`; `:30-32` - `expect(storage.local.toLowerCase()).not.toContain('token')`, `expect(storage.cookies).not.toContain('pt_refresh')` | PASS | re-run at d92feea |

**57 PASS · 8 FAIL** (C8, C13, C15, C19, C20, C21, C54 — all user-deferred; and C10 moves to PASS).
Round 1 was 56/9.

## Coverage

`verified at d92feea` for the rows whose authority the fix touched (`openapi.json` statuses on the
three identity routes; the ordering set; the `forbidden` screen; door 3). All other rows
`carried from round 1`. Members are taken from the authority, not read back from `checks.md`:
route statuses from `src/Api/openapi.json`, screens from the plan's `Observable`, list sort from the
contract's `parameters` blocks.

| Set (size) | Recomputed from | Member -> proof | Unproven | Provenance |
| --- | --- | --- | --- | --- |
| `POST /api/v1/identity/login` statuses (5) | openapi `200,400,401,409,429` | 200 C1 · 400 C3 · 401 C2 · 409 C15 · 429 C13 | - (the login half of C13/C15 is the half that is proven) | **verified at d92feea** - the fix added `429` to the contract; it was already in the plan's `Surface` and already mapped |
| `POST /api/v1/identity/refresh` statuses (6) | openapi `200,401,404,409,429` + plan `400` | 200 C6 · 400 C16 · 401 C6 · 404 C14 | **409** (mapped to C15, which only exercises login) · **429** (mapped to C13, which only exercises login) | **verified at d92feea** - `429` is now contract-declared rather than plan-only; still unproven. *Deferred (achado 3)* |
| `POST /api/v1/identity/logout` statuses (2) | openapi `204,429` | none in this feature | **204 and 429** - no `Surface` row, no `Coverage` row, no check here. `shell.spec.ts:48-52,69-71` asserts it, named by `auth-cookie-contract` C11; statuses by that feature's C5/C6/C19 | **verified at d92feea** - the fix grew this set from 1 member to 2. *Deferred (achado 2)* |
| sort on the 4 list GETs (4) | `openapi.json` `parameters` (`SortBy`,`SortDirection`) + `User.cs:101`, `Role.cs:129`, `Permission.cs:84`, `Tenant.cs:103` | none - `ListQuery` (`core/api.ts:9-13`) has no sort field and `listParams` (`:24-31`) sends none | **all 4** - and the artifacts now assert the set does not exist (`plan.md:43,166,175,180,184`, `STATE.md:15`), which the contract contradicts | **new at d92feea** - round 1 had this as "ordenação por ecrã (4), all 4 unproven"; the fix deleted the rows instead of the gap |
| ecrãs do plano (14) | plan `Observable` + `app.routes.ts` | 14 components, 14 checks; `forbidden` → C10 now PASS | - | **verified at d92feea** - round 1's `forbidden` gap is closed |
| one-way doors do plano (7) | plan `Landing` | VSA C59 · interceptor C4 · **sessão no browser C11 (door now recorded as superseded, `plan.md:266`; C11 proves the shipped shape)** · permissões-do-JWT C9 · cliente à mão C58 · tenant-por-chave C4 · template exclude C63 | - | **verified at d92feea** - round 1's door-3 gap is closed at the `Landing` table; `plan.md:53,206,273` still describe the rejected design (see Binding sources (b)) |
| estados dos 4 ecrãs de lista (12) | `list-state.ts` states × 4 screens | 12 combinations run (`list-state.spec.ts`, `it.each`) | empty-state **label and create action** in all 4 (C19) · the connection-error case in all 4 (C20) | carried from round 1 - *deferred* |
| routes the front consumes (30) | `openapi.json` (30 operations) + `features.json` | 29 have a `Surface` row and a Coverage row | **`POST /api/v1/identity/logout`** | carried from round 1 - *deferred (achado 2)* |
| `POST /api/v1/ai/chat` statuses (3) | openapi | 200 C55 · 401 C57 | **404** - C54 proves the signal, not the navigation removal the claim names | carried from round 1 - *deferred* |
| estados do chat AI (3) | `chat.ts` | pendente C56 | empty-state **copy** "Faça uma pergunta" (`chat.ts:37`) · indisponível → C54 is FAIL | carried from round 1 - *deferred* |
| navegação do shell (5 itens, ordem fixa) | `plan.md:161` + `shell.ts:30-54` | none | **all 5** | carried from round 1 - *deferred (achado 1)* |
| bootstrap: providers HTTP (3 montagens) | read directly: `app.config.ts:10`, `test-providers.ts:24`, `main.ts:4` | `app.config.ts` ✓ · Vitest setup ✓ | the third "montagem" is the same `appConfig` as the first; the row counts two assemblies as three | carried from round 1 - *deferred (achado 6)* |
| permissões que escondem ações (5) | `core/permissions.ts` + `Admin` | 5 rows in `permission.directive.spec.ts` | - | carried from round 1 |
| renovação de token contra a API real (2) | `refresh-coordinator.ts` + `e2e/auth.spec.ts` | interceptor C7 · end-to-end C65 | - | carried from round 1 |
| the 27 other route-status rows | openapi | recompute to the author's mapping | every `403 → C10` cell is now backed end-to-end (redirect **and** screen) | **verified at d92feea** - round 1's inherited C10 gap is gone |

## Test policy rows

`checks.md` carries the same four `Code` rows (the fix did not touch that section). Re-judged this
round: the two round-1 unmet rows, plus any row classifying a file the fix touched. The fix touched
only `screens.spec.ts` and `shell.spec.ts`; `screens.ts` and `shell.ts` are classified by no row —
that is itself a small gap in the Evidence list, but it is the pre-existing shape, not new.

| Row | Files it classifies | Required proof | Expectation met | Provenance |
| --- | --- | --- | --- | --- |
| Decide, atravessado por uma fronteira | `src/app/core/http/api.interceptor.ts` (4 branch points: `!startsWith(API_BASE)`, `403`, `LOCAL_403`, `renewable`) | one at the boundary **and** one at its own level; one asserted case per decision-table row | **no** - improved but still unmet. The `403` row is now asserted all the way to the screen (`screens.spec.ts:12,18`), closing round 1's half-assertion. The `LOCAL_403` branch (`api.interceptor.ts:29`, used by `user-detail.ts:91`) still has **no direct case**: `rg LOCAL_403 src/web/src` returns only `core/api.ts:7`, `api.interceptor.ts:5,29`, `user-detail.ts:9,91` — no spec file | **re-judged at d92feea** - *deferred (achado 5)* |
| Decide, não atravessado por uma fronteira | `src/app/core/session/session.store.ts` (3 branch points) | one at its own level; one case per row | yes - `session.store.spec.ts:14,15,22` | carried from round 1 |
| Decide, não atravessado por uma fronteira | `src/app/shared/problem-details.ts` (3 branch points) | one at its own level; one case per row | yes - `problem-details.spec.ts:65-66`, `:77`, `:91-96` | carried from round 1 |
| Ponto de entrada que não decide | `src/app/features/**` client functions | one at the boundary; accepted input, each rejected input, each error path | **no** - unchanged. Two named error paths still have no case: a list request failing at the connection level (C20) and `GET /roles` / `GET /permissions` without the default page query (C32, C42) | **re-judged at d92feea** - *deferred (achado 5)* |
| Instrumentação, pass-through | `src/app/features/**/*.contracts.ts` | none of its own | yes | carried from round 1 |

## Faults injected

`verified at d92feea`. **Isolation by `git worktree add /private/tmp/r2a-scratch HEAD`**, which works
this round because the tree is committed (round 1 had to back up and restore, since `src/web/` was
untracked). `node_modules` was symlinked in from the real tree; the symlink was removed before
`git worktree remove`.

- Real tree `git status --porcelain` **before**: empty (clean, `HEAD = d92feea`).
- Real tree `git status --porcelain` **after**: empty. Identical. (`.angular/` appeared as an
  untracked build cache from the verifier's own runs and was deleted; nothing tracked moved.)
- Scratch `git status --porcelain` after the last restore: empty.

Faults were placed only on surfaces the fix touched or created — `screens.ts` (behind the new
`screens.spec.ts`) and `shell.ts` (behind the new `shell.spec.ts` case). Four distinct assertion
surfaces, all four previously never made to fail.

| Mutation | Location | Narrowest covering proof | Killed |
| --- | --- | --- | --- |
| forbidden heading `Sem permissão para esta operação` → `Acesso recusado` — **the round-1 survivor, re-injected independently** | `src/web/src/app/shared/screens.ts:33` | `--include src/app/shared/screens.spec.ts --filter "mostra a mensagem de sem permissao"` | **yes** - `screens.spec.ts:12`, `expected 'Acesso recusado Voltar' to contain 'Sem permissão para esta operação'` |
| removed `(click)="back()"` from the forbidden back button (the button renders but does nothing) | `src/web/src/app/shared/screens.ts:34` | same proof | yes - `screens.spec.ts:18`, `expected "back" to be called 1 times, but got 0 times` |
| not-found link label `Voltar` → `Regressar` | `src/web/src/app/shared/screens.ts:13` | `--include src/app/shared/screens.spec.ts --filter "mostra o titulo recebido"` | yes - `screens.spec.ts:29-31`, `expected 'Regressar' to be 'Voltar'` |
| logout ignores a cancelled confirmation (`if (!confirmed) return;` disabled) | `src/web/src/app/shell/shell.ts:96` | `--include src/app/shell/shell.spec.ts --filter "cancelar o dialogo nao termina a sessao"` | yes - `shell.spec.ts:69`, `expected [ { method: 'POST', …(3) } ] to have a length of +0 but got 1` |

4 injected, 4 killed, 0 survived. Round 1's only surviving mutant is dead.

## Swept

`carried from round 1`, re-read against the code at `d92feea`; the section was not touched by the fix.

- **`data lifecycle: C11 - as chaves pt.auth e pt.tenant são removidas no logout`** - still false.
  `session.store.ts:79-83` (`clear()`) removes only `AUTH_STORAGE_KEY`; `pt.tenant` is deliberately
  kept and `session.store.spec.ts:53` asserts it survives
  (`expect(localStorage.getItem(TENANT_STORAGE_KEY)).toBe('dev')`). The row states the opposite of
  what its own check proves. *Deferred by the user (achado 4).*
- `idempotency / concurrency: C7` - holds; the shared in-flight queue is at
  `refresh-coordinator.ts:25-27,37` and round 1's mutation confirmed the assertion catches its removal.
- `observability: n/a` - approved policy; nothing in the code for it to be wrong about.
- The remaining rows resolve to check IDs, not to `existing` constraints.

## Cross-feature note (the fix's blast radius)

The fix edited `.specs/features/auth-cookie-contract/checks.md`, `src/Api/Features/Identity/*.cs`,
`src/Api/openapi.json`, `tests/ArchitectureTests/SolutionFileTests.cs` and
`tests/E2ETests/Identity/IdentityAuthE2ETests.cs` — outside this feature's verdict, but checked for
spillover: `auth-cookie-contract` C11 carries **both** proofs for `shell.spec.ts` (including the new
`cancelar o dialogo nao termina a sessao`), so the shared spec file the fix extended is correctly
named there. The new `NotFound` test (`screens.spec.ts:23`) is named by **no check in either
feature** — extra coverage that no artifact would notice the loss of.

## Walk the flow with the user

Not run, again. Step 5 applies (user-facing UI) but a sub-agent verifier has no channel to the user.
Logged as **not run**, not as passed. *Deferred by the user (achado 10).*

## Ranked gaps

1. **The four ordering rows were removed on a false premise.** `plan.md:43,166,175,180,184` and
   `.specs/STATE.md:15` (AD-006) now state the API exposes no sort. `src/Api/openapi.json` declares
   `SortBy` and `SortDirection` on all four list GETs, and `User.cs:101`, `Role.cs:129`,
   `Permission.cs:84`, `Tenant.cs:103` implement them — with `createdAt` desc as the default, which
   is exactly the ordering `plan.md:166` used to promise. The decision not to sort in the front is
   the user's to make; the artifact must not justify it with a claim the binding contract
   contradicts, because the next build reads the artifact.
   - `src/Api/Features/Identity/User.cs:101`
2. **C1 now contradicts its own AC.** The check was rewritten to the shipped shape (`tenantKey` +
   `user`, no token) and cites `AC 1`, which at `plan.md:53` still says `refreshToken` goes to
   `localStorage`. `plan.md:206` (Flow 4) and `plan.md:273` (Impact) say the same. Only the `Landing`
   door line got its supersession note (`plan.md:266`).
   - `.specs/features/web-frontend/plan.md:53`
3. **Seven checks still name two behaviours and prove one** - C8, C13, C15, C19, C20, C21, C54.
   *User-deferred (achado 1, 3); listed so the count is not lost.*
   - `src/web/src/app/core/http/refresh-coordinator.ts:51`, `src/web/src/app/features/identity/login.ts:139,145`,
     `src/web/src/app/features/identity/users-list.ts:197`, `src/web/src/app/shell/shell.ts:52`,
     `src/web/src/app/shared/list-state.spec.ts:107`
4. **Five element groups the plan's `Observable` decides still have no check**, including the one
   arrangement decision (shell navigation order and membership). *User-deferred (achado 1).*
   - `src/web/src/app/shell/shell.ts:30-54`
5. **`POST /api/v1/identity/logout` still has no `Surface` row, no `Coverage` row and no check here**,
   and the fix grew its contract status set from 1 to 2. *User-deferred (achado 2).*
   - `src/web/src/app/shell/shell.ts:102`
6. **Two `Test policy` rows remain unmet** - `LOCAL_403` has no direct case; two entry-point error
   paths have none. *User-deferred (achado 5).*
   - `src/web/src/app/core/http/api.interceptor.ts:29`
7. **The `Swept` data-lifecycle row states the opposite of what its own check proves.**
   *User-deferred (achado 4).*
   - `src/web/src/app/core/session/session.store.spec.ts:53`
8. **The runner exits 0 on a filter that matches nothing** (`Tests 4 skipped (4)`, exit 0). No proof
   command is affected today — all 64 were matched against the real test inventory — but the
   rewritten commands carry no guard against a future rename.
   - `.specs/features/web-frontend/checks.md:48`

## Gate

`python3 scripts/validate_verification.py web-frontend --root /Users/luissoares/Repos/Feature.template` - exit 1, 1 error: the verdict is FAIL.

Proof totals behind the verdict, all at `d92feea`: `ng test --no-watch` **85 passed / 0 failed**
(20 files); `playwright test` 2 passed / 0 failed; `dotnet test tests/ArchitectureTests --filter
"FullyQualifiedName~TemplateConfigTests"` 2 passed / 0 failed; `tsc --noEmit` exit 0 on both project
references. Faults: 4 injected, 4 killed.
