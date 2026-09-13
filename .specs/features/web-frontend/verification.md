# Web front-end (Angular 22) verification

**Verdict**: FAIL
**Profile**: ui
**Diff range**: 902d206..working tree (uncommitted)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

9 of 65 checks are not proven. One of them (C1) states the opposite of what the versioned
contract and the code do, and its own named test asserts the contradiction. One mutation
survived the whole suite. Eleven elements the plan's `Observable` section decides have no check
at all, and two enumerations (`POST /api/v1/identity/logout`; refresh `409`/`429`) have no
coverage row.

## How the proofs were run

Every proof command in `checks.md` is written as `cd src/web && npx vitest run <file> -t "<name>"`.
**None of them works as written.** There is no `vitest.config.*` in `src/web`, so raw `vitest`
runs with no jsdom environment and no Angular TestBed: the first `beforeEach` in
`src/test-setup.ts:17` throws `ReferenceError: localStorage is not defined` before any assertion
executes. Reproduced on C1:

```
$ cd src/web && npx vitest run src/app/features/identity/login.spec.ts -t "guarda a sessao e navega para users"
 FAIL  src/app/features/identity/login.spec.ts > Login > guarda a sessao e navega para users
ReferenceError: localStorage is not defined
 ❯ src/test-setup.ts:17:3
```

The real runner is the `@angular/build:unit-test` builder configured in `angular.json`. Proofs were
therefore run as `npx ng test --watch=false --reporters verbose [--include <file> --filter <name>]`,
which is what `npm test` invokes. One invocation covered all 19 spec files: **82 tests, 82 passed,
0 failed**, every named test shown individually in the verbose output. Playwright was run once for
both e2e proofs (`PW_CHANNEL=chrome`, dev server + real API on `:5080`, in-memory DB), and
`dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~TemplateConfigTests"` once for C63.

Every test named by a check exists and ran. No filter matched nothing.

## Binding sources

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| `src/Api/openapi.json` (versioned contract, 30 paths) | yes - all 30 paths + `AuthTokenResponse` schema read | **C1 says login stores `refreshToken` in `localStorage['pt.auth']`. `AuthTokenResponse` has no `refreshToken` property and `src/Api/Features/Identity/Login.cs:98` writes it to the `pt_refresh` cookie. The plan's door 3 (`plan.md:259`) records the cookie as the *rejected* alternative.** | `POST /api/v1/identity/logout` - in the contract, called by `shell.ts:102`, absent from the plan's `Surface` table and from every `Coverage` row |
| `features.json` (30 slices / 30 routes) | yes - parsed, all routes listed | plan says "o índice das 29 rotas" (`plan.md:6`) and the `Surface` table carries 29; the index now has 30 | `Logout` slice has no check |
| `docs/security/RBAC_MATRIX.md` (11 policies) | yes - policy table + route tables read | none - `core/permissions.ts:3-10` reproduces all 8 canonical permission names exactly | - |
| `docs/architecture/vsa.md` · `.cursor/rules/architecture-vsa.mdc` | yes | none - `features/{module}/{slice}.ts` flat files, enforced by C59 | - |
| plan `Observable` + `src/web/src/app/app.routes.ts` (14 screens / 14 router paths) | yes - every row of `Observable` compared against the component that renders it | plan `Surface` (`plan.md:219`) lists the front's URLs but omits `/tenants/new` (`app.routes.ts:58`) and the `''`→`users` redirect (`app.routes.ts:17`). Plan says user-form loading is a "skeleton nos campos" (`plan.md:169`); `user-form.ts:36` renders a `mat-progress-bar` | **11 elements - enumerated below** |

### Elements and arrangement the design decides that no check reaches

Each one is implemented in the code and reachable by a selector. None has a check, so each would
survive a rewrite. `checks.md` carries no exemption clause for visual fidelity, so nothing here is
out of reach by declaration.

| Screen | Element the plan decides | Where it lives | Check |
| --- | --- | --- | --- |
| `shell` | navigation **order and membership**: fixed Utilizadores, Roles, Permissões, Tenants, AI (`plan.md:161`) | `shell.ts:30-54`, five `data-testid="nav-*"` | none |
| `shell` | nav items without permission are not rendered (`plan.md:159`) | `shell.ts:32,39,46` | only C25, on a synthetic `Host` component (`permission.directive.spec.ts:11`), never on the shell |
| `shell` | Sair asks for confirmation in the shared dialog (`plan.md:160`) | `shell.ts:91-95` | none - `shell.spec.ts:36` stubs the dialog and asserts nothing about it |
| `login` | submit disabled while the request runs (`plan.md:153`) | `login.ts:54` `[disabled]="pending()"` | none |
| `users-list` | default ordering `createdAt` desc (`plan.md:166`, Assumptions row marked Confirmed `y`) | nowhere - `ListQuery` (`core/api.ts:9-13`) has no sort field and no list endpoint sends one | none |
| `roles-list` | ordering by name asc (`plan.md:175`) | nowhere | none |
| `permissions-list` | ordering by `name` asc (`plan.md:180`) | nowhere | none |
| `tenants-list` | ordering by `tenantKey` asc (`plan.md:184`) | nowhere | none |
| `tenants-list` | deactivate confirmation says it is reversible by editing (`plan.md:183`) | `tenants-list.ts:148` `Desativar ${tenant.tenantKey}? Pode reativar pela edição.` | none |
| `ai-chat` | empty state reads "Faça uma pergunta" (`plan.md:186`) | `chat.ts:37` | C55 asserts only that `chat-empty` exists (`chat.spec.ts:38`) |
| `forbidden` | message "Sem permissão para esta operação" + button back to the previous route (`plan.md:190`) | `screens.ts:33-36` | none - see the surviving mutant below |
| list screens ×4 | empty-state labels "Nenhum utilizador"/"Nenhum role"/"Nenhuma permissão"/"Nenhum tenant" (`plan.md:162,169,178,181`) | `users-list.ts:71`, `roles-list.ts:164`, `permissions-list.ts:70`, `tenants-list.ts:50` | C19 asserts only that the `list-empty` container exists |
| document `src/web/AGENTS.md` | documents the layer-folder rule (`plan.md:197`) | `src/web/AGENTS.md` exists and documents it | none |

## Checks

65 check IDs (C1..C65, none missing). `checks.md:7` says "64 checks" - off by one.

Verdict rule used: PASS when every behaviour, element, status or label the check names has a
located assertion; FAIL when one of them has no assertion anywhere in the tree. Sub-value omissions
inside an otherwise asserted behaviour are recorded as notes, not FAILs.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | login stores `refreshToken`+`tenantKey` in `localStorage['pt.auth']` | `ng test --include login.spec.ts --filter "guarda a sessao e navega para users"` exit 0 | `src/web/src/app/features/identity/login.spec.ts:40` - `expect(Object.keys(stored).sort()).toEqual(['tenantKey', 'user'])` and `:43` - `expect(raw.toLowerCase()).not.toContain('token')` | FAIL - the assertion proves the refresh token is **not** stored, the opposite of the claim |
| C2 | `401` keeps `/login`, shows `detail`, preserves email | same file | `login.spec.ts:60` - `expect(text(fixture,'login-message')).toBe('Invalid email or password.')`; `:63-64` - email input `.value).toBe('admin@producttemplate.com')` | PASS |
| C3 | `400` maps `errors[field]` under each field | `--include problem-details.spec.ts` | `src/web/src/app/shared/problem-details.spec.ts:65-66` - `expect(text(fixture,'email-error')).toBe("'Email' is not a valid email address.")` | PASS |
| C4 | every `/api/v1/**` request carries `X-Tenant` | `--include api.interceptor.spec.ts` | `src/web/src/app/core/http/api.interceptor.spec.ts:42` - `expect(requests.at(-1)?.headers.get('X-Tenant')).toBe('acme')` | PASS |
| C5 | `Authorization: Bearer <accessToken>` | same file | `api.interceptor.spec.ts:71` - `expect(requests.at(-1)?.headers.get('Authorization')).toBe(\`Bearer ${session.accessToken()}\`)` | PASS |
| C6 | `401` → exactly one refresh + retry; refresh `401` clears + `/login` | same file | `api.interceptor.spec.ts:93-94` - `expect(requests.filter(r=>r.url.pathname===REFRESH)).toHaveLength(1)` / `...===USERS)).toHaveLength(2)`; `:106-108` - `expect(router.url).toContain('/login')`, `expect(session.accessToken()).toBeNull()` | PASS |
| C7 | three parallel `401` → one refresh | same file | `api.interceptor.spec.ts:160` - `expect(requests.filter(r=>r.url.pathname===REFRESH)).toHaveLength(1)` | PASS - note: "as três repetidas com o mesmo token" is not asserted, only that all three resolve |
| C8 | protected route → `/login?redirectTo=<rota>`, then back after login | `--include guards.spec.ts` | `src/web/src/app/core/guards/guards.spec.ts:13` - `expect(router.url).toBe('/login?redirectTo=%2Ftenants')` | FAIL - the second clause is performed by the test itself (`guards.spec.ts:16-17` reads `redirectTo` and calls `navigateByUrl`); `login.ts:139-140`, the code that honours it, has no assertion |
| C9 | `hasPermission` reads the `permission` claim | `--include session.store.spec.ts` | `src/web/src/app/core/session/session.store.spec.ts:14-15` - `expect(session.hasPermission('identity.user.read')).toBe(true)` / `('identity.user.manage')).toBe(false)` | PASS |
| C10 | `403` renders `forbidden` with "Sem permissão para esta operação" and a back button | `--include api.interceptor.spec.ts` | `api.interceptor.spec.ts:169` - `expect(router.url).toBe('/forbidden')` | FAIL - no assertion anywhere on the message (`screens.ts:33`) or the button (`screens.ts:34`); `rg "Sem permiss" src e2e` returns only the source. Confirmed by a surviving mutant |
| C11 | logout clears `pt.auth`, empties the in-memory token, navigates to `/login` | `--include session.store.spec.ts` | `session.store.spec.ts:50` - `expect(session.accessToken()).toBeNull()`; `:52` - `expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull()`; navigation at `src/web/src/app/shell/shell.spec.ts:52` - `expect(router.url).toBe('/login')` | PASS - note: the navigation clause is settled by a test the check does not name |
| C12 | top bar shows `firstName` and `tenantKey` | `--include shell.spec.ts` | `shell.spec.ts:29-30` - `expect(text(fixture,'session-user')).toBe('System')` / `('session-tenant')).toBe('dev')` | PASS |
| C13 | `429` shows the rate-limit message and keeps the fields filled | `--include login.spec.ts` | `login.spec.ts:74` - `expect(text(fixture,'login-message')).toBe(RATE_LIMIT_MESSAGE)` (`login.ts:14` = "Demasiadas tentativas, tente dentro de um minuto") | FAIL - "mantém os campos preenchidos" has no assertion and is contradicted by `login.ts:145`, which clears the password on every error |
| C14 | refresh `404` clears the session and navigates to `/login` | `--include api.interceptor.spec.ts` | `api.interceptor.spec.ts:120-121` - `expect(router.url).toContain('/login')`, `expect(session.accessToken()).toBeNull()` | PASS |
| C15 | login **or refresh** `409` shows "Tenant inválido" and stores no session | `--include login.spec.ts` | `login.spec.ts:92-93` - `expect(text(fixture,'tenant-error')).toBe(INVALID_TENANT_MESSAGE)`, `expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull()` | FAIL - only the login case is exercised; the refresh-`409` path (`refresh-coordinator.ts:51`, `reasonFor` → `'tenant'`) has no assertion, and the Coverage table maps refresh `409` to this check |
| C16 | refresh `400` clears the session and navigates to `/login` | `--include api.interceptor.spec.ts` | `api.interceptor.spec.ts:133-134` - `expect(router.url).toContain('/login')`, `expect(session.accessToken()).toBeNull()` | PASS |
| C17 | `/users` issues `?pageNumber=1&pageSize=20`, four columns | `--include users-list.spec.ts` | `src/web/src/app/features/identity/users-list.spec.ts:48-49` - `expect(query?.get('pageNumber')).toBe('1')` / `('pageSize')).toBe('20')`; `:54` - `expect(headers).toEqual(['Email','Nome','Criado em','Último login',''])` | PASS |
| C18 | loading shows `mat-progress-bar`, paginator disabled, 4 screens | `--include list-state.spec.ts --filter "estado de carregamento"` (4 rows) | `src/web/src/app/shared/list-state.spec.ts:67-70` - `expect(maybeEl(fixture,'list-loading')).not.toBeNull()` and paginator `.disabled).toBe(true)`; `list-state.ts:15` binds `list-loading` to `<mat-progress-bar>` | PASS |
| C19 | `totalCount === 0` shows the empty state **with the create action**, 4 screens | `--filter "estado vazio"` (4 rows) | `list-state.spec.ts:88` - `expect(maybeEl(fixture,'list-empty')).not.toBeNull()` | FAIL - the create action (`list-state.ts:20` `<ng-content select="[emptyAction]">`, `users-list.ts:74`) and the per-screen label have no assertion |
| C20 | `500` **or connection error** shows the `title` and "Tentar de novo" re-runs the query, 4 screens | `--filter "estado de erro repete a query"` (4 rows) | `list-state.spec.ts:107` - `expect(text(fixture,'list-error-title')).toBe('Unexpected error')`; `:112` - `expect(calls).toBe(2)` | FAIL - only the `500` case is exercised; the connection-error case named in the check (and in `Swept: failure modes`) has no assertion |
| C21 | paging/search **writes** `pageNumber`, `pageSize`, `searchTerm` to the URL and reloads from them | `--include users-list.spec.ts` | `users-list.spec.ts:68-70` - `expect(query?.get('pageNumber')).toBe('2')` etc., fed from a pre-set route stub | FAIL - proves only the read direction; `users-list.ts:197-205` (`router.navigate` with `queryParams`) has no assertion |
| C22 | create → `POST register`, `201` → `/users` + snackbar "Utilizador criado" | `--include user-form.spec.ts` | `src/web/src/app/features/identity/user-form.spec.ts:50-51` - `expect(router.url).toBe('/users')`, `expect(document.body.textContent).toContain(USER_CREATED_MESSAGE)` (`user-form.ts:16` = 'Utilizador criado') | PASS |
| C23 | confirm delete → `DELETE`, `204` → row gone | `--include users-list.spec.ts` | `users-list.spec.ts:88` - `expect(maybeEl(fixture,'row-user-1')).toBeNull()` | PASS |
| C24 | cancel emits no HTTP request | same file | `users-list.spec.ts:105` - `expect(requests.length).toBe(before)` | PASS |
| C25 | management actions hidden without the permission and without `Admin`, 5 cases | `--include permission.directive.spec.ts` (5 rows) | `src/web/src/app/core/session/permission.directive.spec.ts:33` - `expect(maybeEl(fixture,'action')).toBeNull()`; `:38` - `.not.toBeNull()` after granting | PASS |
| C26 | `/users/{id}` issues both GETs and shows both | `--include user-detail.spec.ts` | `src/web/src/app/features/identity/user-detail.spec.ts:37-38` - `expect(text(fixture,'user-email')).toBe('ana@example.com')`, `expect(text(fixture,'roles-list')).toContain('Auditor')` | PASS |
| C27 | save → `PUT`, `200` → fields show the response | `--include user-form.spec.ts` | `user-form.spec.ts:94-95` - `expect(el(fixture,'firstName').value).toBe('Ana Maria')` / `('lastName').value).toBe('Silva Costa')` | PASS |
| C28 | `404` on a detail GET renders not-found with the `title` | `--include problem-details.spec.ts` | `problem-details.spec.ts:77` - `expect(problemKind(problem)).toBe('not-found')`; `:83` - `expect(text(fixture,'not-found-title')).toBe('Not found')` | PASS |
| C29 | register `409` marks the email field and keeps the form filled | `--include user-form.spec.ts` | `user-form.spec.ts:72-73` - `expect(text(fixture,'email-error')).toBe("Email 'ana@example.com' is already registered.")`, `expect(el(fixture,'firstName').value).toBe('Ana')` | PASS |
| C30 | `404` on PUT/DELETE → "Registo não encontrado" + list reload | `--include problem-details.spec.ts` | `problem-details.spec.ts:91-92` - `expect(handleMutationError(notFound, reload)).toBe(NOT_FOUND_MESSAGE)`, `expect(reload).toHaveBeenCalledTimes(1)` | PASS - note: the snackbar rendering (`users-list.ts:190`) is not asserted |
| C31 | `403` on users roles → "Sem acesso aos roles", rest of the screen intact | `--include user-detail.spec.ts` | `user-detail.spec.ts:54-56` - `expect(text(fixture,'roles-denied')).toBe(NO_ROLE_ACCESS_MESSAGE)`, `expect(text(fixture,'user-email')).toBe(...)`, `expect(maybeEl(fixture,'forbidden')).toBeNull()` | PASS |
| C32 | `/roles` issues `?pageNumber=1&pageSize=20`, name+description per row | `--include roles-list.spec.ts` | `src/web/src/app/features/authorization/roles-list.spec.ts:45-46` - `expect(text(fixture,'row-role-1')).toContain('Admin')`, `expect(text(fixture,'description-role-1')).toBe('Acesso total')` | PASS - note: unlike C17, the query params are not asserted |
| C33 | `/roles/{id}` shows name and description | `--include role-detail.spec.ts` | `src/web/src/app/features/authorization/role-detail.spec.ts:47-48` - `expect(text(fixture,'role-name')).toBe('Auditor')` / `('role-description')).toBe('Leitura')` | PASS |
| C34 | detail lists assigned permissions by `name` | same file | `role-detail.spec.ts:55` - `expect(text(fixture,'role-permissions')).toContain('identity.user.read')` | PASS |
| C35 | assign → `POST`, `204` → appears without a new read | same file | `role-detail.spec.ts:79-80` - `expect(maybeEl(fixture,\`permission-${MANAGE.id}\`)).not.toBeNull()`, `expect(requests.filter(r=>r.method==='GET').length).toBe(reads)` | PASS |
| C36 | revoke → `DELETE`, `204` → leaves the list | same file | `role-detail.spec.ts:97-98` - `expect(maybeEl(fixture,'permission-perm-1')).toBeNull()`, `expect(text(fixture,'permissions-empty')).toBe('Sem permissões atribuídas')` | PASS |
| C37 | create role → `POST`, `201` → row added, dialog closes | `--include roles-list.spec.ts` | `roles-list.spec.ts:69-70` - `expect(text(fixture,'row-role-2')).toContain('Auditor')`, `expect(dialog.openDialogs).toHaveLength(0)` | PASS |
| C38 | role `409` keeps the dialog open with `detail` at the Nome field | same file | `roles-list.spec.ts:94-95` - `expect(overlayEl('name-error').textContent?.trim()).toBe("Role 'Admin' already exists.")`, `expect(dialog.openDialogs).toHaveLength(1)` | PASS |
| C39 | save role → `PUT`, `200` → row updated | same file | `roles-list.spec.ts:115` - `expect(text(fixture,'row-role-1')).toContain('Administrador')` | PASS |
| C40 | delete role → `DELETE`, `204` → row gone | same file | `roles-list.spec.ts:129` - `expect(maybeEl(fixture,'row-role-1')).toBeNull()` | PASS |
| C41 | assign/revoke user role → POST/DELETE, each followed by a GET | `--include user-roles.spec.ts` | `src/web/src/app/features/authorization/user-roles.spec.ts:53-55` - `expect(requests.filter(r=>r.method==='POST'&&r.url.pathname===ASSIGNED)).toHaveLength(1)`; `:61-63` - `...method==='GET'...)).toHaveLength(3)` | PASS |
| C42 | `/permissions` issues the GET, name+description per row | `--include permissions-list.spec.ts` | `src/web/src/app/features/authorization/permissions-list.spec.ts:45-46` - `expect(text(fixture,'row-perm-1')).toBe('identity.user.read')`, `expect(text(fixture,'description-perm-1')).toBe('Ler utilizadores')` | PASS |
| C43 | create permission → `POST`, `201` → appears | same file | `permissions-list.spec.ts:68` - `expect(text(fixture,'row-perm-2')).toBe('tenants.read')` | PASS |
| C44 | permission `409` keeps the dialog open with `detail` at Nome | same file | `permissions-list.spec.ts:92-95` - `expect(overlayEl('name-error').textContent?.trim()).toBe("Permission 'tenants.read' already exists.")`, `expect(dialog.openDialogs).toHaveLength(1)` | PASS |
| C45 | save permission → `PUT`, `200` → row updated | same file | `permissions-list.spec.ts:114` - `expect(text(fixture,'row-perm-1')).toBe('identity.user.list')` | PASS |
| C46 | delete permission → `DELETE`, `204` → row gone | same file | `permissions-list.spec.ts:128` - `expect(maybeEl(fixture,'row-perm-1')).toBeNull()` | PASS |
| C47 | `/tenants` issues `?pageNumber=1&pageSize=20`, four columns | `--include tenants-list.spec.ts` | `src/web/src/app/features/tenants/tenants-list.spec.ts:51-55` - `expect(requests.at(-1)?.url.searchParams.get('pageSize')).toBe('20')`, then `row/name/active/isolation` text assertions | PASS - note: `pageNumber=1` is not asserted |
| C48 | create tenant → `POST`, `201` → `/tenants` | `--include tenant-form.spec.ts` | `src/web/src/app/features/tenants/tenant-form.spec.ts:44` - `expect(router.url).toBe('/tenants')` | PASS |
| C49 | tenant `409` keeps the form filled, `detail` at the Chave field | same file | `tenant-form.spec.ts:66-67` - `expect(text(fixture,'tenantKey-error')).toBe("Tenant key 'qa' is already taken.")`, `expect(el(fixture,'displayName').value).toBe('Quality')` | PASS |
| C50 | save tenant → `PUT`, `200` → fields show the response | same file | `tenant-form.spec.ts:85` - `expect(text(fixture,'field-displayName')).toBe('Dev Environment')` | PASS |
| C51 | tenant `PUT` `400` maps `errors[field]` under each field | same file | `tenant-form.spec.ts:106` - `expect(text(fixture,'displayName-error')).toBe("'Display Name' must not be empty.")` | PASS |
| C52 | deactivate → `DELETE`, `204` → row shows inactive | `--include tenants-list.spec.ts` | `tenants-list.spec.ts:69` - `expect(text(fixture,'active-tenant-1')).toBe('Não')` | PASS |
| C53 | `/tenants/{id}` shows the seven `TenantOutput` fields | `--include tenant-form.spec.ts` | `tenant-form.spec.ts:117-123` - seven `expect(text(fixture,'field-*'))` assertions, matching the seven required properties of `TenantOutput` in `src/Api/openapi.json` | PASS |
| C54 | chat `404` "Feature disabled" **removes the AI item from the navigation** | `--include chat.spec.ts` | `src/web/src/app/features/ai/chat.spec.ts:91-92` - `expect(TestBed.inject(AiAvailability).available()).toBe(false)`, `expect(text(fixture,'chat-unavailable')).toBe(AI_UNAVAILABLE_MESSAGE)` | FAIL - level gap: the claim is about the navigation; the assertion sits one level below it, on the signal. `shell.ts:52-54` (`@if (ai.available())` around `data-testid="nav-ai"`) has no assertion |
| C55 | send appends to history, `POST` with `message`+`history`, renders `reply` | same file | `chat.spec.ts:43-44` - `expect(received).toEqual({ message: 'olá', history: [] })`, `expect(text(fixture,'chat-history')).toContain('Olá!')` | PASS |
| C56 | pending disables Send and shows the typing indicator | same file | `chat.spec.ts:67-68` - `expect(el(fixture,'chat-send').disabled).toBe(true)`, `expect(maybeEl(fixture,'chat-typing')).not.toBeNull()` | PASS |
| C57 | chat `401` goes through the refresh path before any error | same file | `chat.spec.ts:118-120` - `expect(refreshCalls).toBe(1)`, `expect(text(fixture,'chat-history')).toContain('renovado')`, `expect(maybeEl(fixture,'chat-error')).toBeNull()` | PASS |
| C58 | a `features.json` route with no client fails `npm test` | `--include architecture.spec.ts` | `src/web/src/app/architecture.spec.ts:46` - `expect(missing).toEqual([])`, over all 30 entries of `features.json` | PASS |
| C59 | a layer folder under `features/` fails `npm test` | same file | `architecture.spec.ts:81` - `expect(walk(featuresRoot)).toEqual([])` against `['services','components','models','pages','dtos','interfaces']` | PASS |
| C60 | `npm run e2e` logs in, lists, creates and deletes against `:5080` with tenant `dev` | `PW_CHANNEL=chrome npx playwright test e2e/users.spec.ts e2e/auth.spec.ts -g "cria e elimina um utilizador\|renova o token expirado"` - 2 passed | `src/web/e2e/users.spec.ts:20` - `await expect(row).toHaveCount(1)`; `:25` - `await expect(page.locator('tr',{hasText:email})).toHaveCount(0)`; tenant from `e2e/fixtures.ts:5,9` | PASS |
| C61 | `web-e2e` publishes `playwright-report` with `if: always()` | `--include architecture.spec.ts` | `architecture.spec.ts:96-98` - `expect(job).toContain('actions/upload-artifact')` / `('playwright-report')` / `('if: always()')`; confirmed in `.github/workflows/ci.yml` | PASS |
| C62 | `tsconfig.json` declares `strict: true` and `tsc --noEmit` exits 0 | `--include architecture.spec.ts` + `npx tsc -p tsconfig.app.json --noEmit` (exit 0) and `-p tsconfig.spec.json --noEmit` (exit 0), run by the verifier | `architecture.spec.ts:89` - `expect(tsconfig.compilerOptions.strict).toBe(true)`; `src/web/tsconfig.json:13` `"strict": true` | PASS - note: the named proof settles only the first half; the `tsc` half has no assertion inside `npm test` |
| C63 | `template.json` excludes `**/node_modules/**` and `**/dist/**` | `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~TemplateConfigTests"` - 2 passed | `tests/ArchitectureTests/TemplateConfigTests.cs:42` - `Assert.Contains(glob, excludes)`, run once per glob (`:26-27`), both shown in the output | PASS |
| C64 | HTTP providers registered in all three assemblies | `--include architecture.spec.ts` | `architecture.spec.ts:107` - `expect(assembly).toContain('provideHttpClient(withInterceptors([apiInterceptor]))')` over `app.config.ts` and `test-providers.ts`; assemblies read directly at `src/web/src/app/app.config.ts:10` and `src/web/src/test-providers.ts:24` | PASS - note: the "third assembly" is asserted as `expect(playwright).toContain('npm run start')` (`:111`), a proxy; `main.ts:4` bootstraps the same `appConfig`, so there are two distinct assemblies, not three |
| C65 | a session with no in-memory access token renews from the refresh cookie without returning to login | Playwright, same invocation as C60 | `src/web/e2e/auth.spec.ts:18-20` - `await expect(page.getByTestId('users-table')).toBeVisible()`, `expect(refreshCalls.length).toBeGreaterThan(0)`; `:30-32` - `expect(storage.local.toLowerCase()).not.toContain('token')`, `expect(storage.cookies).not.toContain('pt_refresh')` | PASS |

**56 PASS · 9 FAIL** (C1, C8, C10, C13, C15, C19, C20, C21, C54).

## Coverage

Recomputed from the authorities, not read back from `checks.md`. Route members come from
`src/Api/openapi.json` (30 paths) plus the statuses the plan's `Surface` adds from
`RequireAuthorization` and the `auth` rate limiter; screens come from the plan's `Observable`
section plus `src/web/src/app/app.routes.ts`.

Only rows whose recomputation changes something are listed individually; the 27 route rows not
named here recompute to the author's mapping and their members are proven, except that every
`403 → C10` cell inherits C10's gap (the `403` redirect is proven, the screen it lands on is not).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| routes the front consumes (30) | `src/Api/openapi.json` paths + `features.json` | 29 have a `Surface` row and a Coverage row | **`POST /api/v1/identity/logout`** - in the contract, in `features.json`, called at `shell.ts:102`, asserted at `shell.spec.ts:49-50`, and given no row by either artifact |
| `POST /api/v1/identity/refresh` statuses (6) | openapi (`200,401,404,409`) + plan (`400`, `429`) | 200 C6 · 400 C16 · 401 C6 · 404 C14 | **409** (mapped to C15, which only exercises login) · **429** (mapped to C13, which only exercises login) |
| `POST /api/v1/ai/chat` statuses (3) | openapi | 200 C55 · 401 C57 | **404** - C54 proves the signal, not the navigation removal the claim names |
| ecrãs do plano (14) | plan `Observable` + `app.routes.ts` | 14 components, 14 checks | `forbidden` → C10 is FAIL, so the screen's own contents are unproven |
| estados dos 4 ecrãs de lista (12) | `list-state.ts` states × 4 screens | 12 combinations run (`list-state.spec.ts`, `it.each`) | empty-state **label and create action** in all 4 (C19) · the connection-error case in all 4 (C20) |
| one-way doors do plano (7) | plan `Landing` | VSA C59 · interceptor C4 · permissions-from-JWT C9 · hand-written client C58 · tenant-by-key C4 · template exclude C63 | **door 3 (session in the browser)** - `plan.md:259` still fixes `localStorage['pt.auth'] = { refreshToken, tenantKey }`; the door was reopened and reversed by `.specs/features/auth-cookie-contract`, and C11 now proves the opposite shape |
| permissões que escondem ações (5) | `core/permissions.ts` manage names + `Admin` | 5 rows in `permission.directive.spec.ts` | - |
| estados do chat AI (3) | `chat.ts` | pendente C56 | empty-state **copy** "Faça uma pergunta" (`chat.ts:37`) · indisponível → C54 is FAIL |
| bootstrap: providers HTTP (3 montagens) | read directly: `app.config.ts:10`, `test-providers.ts:24`, `main.ts:4` | `app.config.ts` ✓ · Vitest setup ✓ | the third "montagem" is the same `appConfig` as the first; the row counts two assemblies as three |
| renovação de token contra a API real (2) | `refresh-coordinator.ts` + `e2e/auth.spec.ts` | interceptor C7 · end-to-end C65 | - |
| ordenação por ecrã (4) | plan `Observable` rows `plan.md:166,175,180,184` | none | **all 4** - `ListQuery` (`core/api.ts:9-13`) has no sort field, no list endpoint sends one, and no check names one. The set has no row in `checks.md` at all |
| navegação do shell (5 itens, ordem fixa) | plan `plan.md:161` + `shell.ts:30-54` | none | **all 5** - no check reaches the shell navigation |

## Test policy rows

`checks.md` carries four rows. Verdicts below.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decide, atravessado por uma fronteira | `src/app/core/http/api.interceptor.ts` (4 branch points: `!startsWith(API_BASE)`, `403`, `LOCAL_403`, `renewable`) | one at the boundary **and** one at its own level; the contract at the boundary, one asserted case per decision-table row at its own level | **no** - the own-level proofs exist (`api.interceptor.spec.ts`, 9 tests) and cover `X-Tenant`, `Authorization`, `withCredentials`, `401` renew/retry, parallel `401`, refresh `400/401/404`, and the `403` redirect. But the `LOCAL_403` branch (`api.interceptor.ts:29`, the opt-out used by `user-detail.ts`) has no direct case, and the `403` row is asserted only as far as the URL - the screen the branch exists to render is not. One row of the decision table is half-asserted and one is unasserted |
| Decide, não atravessado por uma fronteira | `src/app/core/session/session.store.ts` (3 branch points: `Admin` role, claim present, claim absent) | one at its own level; one asserted case per row | yes - `session.store.spec.ts:14,15,22` cover all three |
| Decide, não atravessado por uma fronteira | `src/app/shared/problem-details.ts` (3 branch points: `400`/`404`/generic) | one at its own level; one asserted case per row | yes - `problem-details.spec.ts:65-66` (`400`), `:77` (`404`), `:91-96` (`404` and the `409` fall-through) |
| Ponto de entrada que não decide | `src/app/features/**` client functions | one at the boundary; accepted input, each rejected input, each error path | **no** - error paths are covered per slice through MSW, but two named error paths have no case anywhere: a list request that fails at the connection level (C20) and `GET /roles` / `GET /permissions` without the default page query (C32) |
| Instrumentação, pass-through | `src/app/features/**/*.contracts.ts` | none of its own | yes |

## Faults injected

**Isolation was by backup-and-restore, not by `git worktree add <scratch> HEAD`.** `HEAD` is
`902d206`, which predates the entire feature: `src/web/` does not exist there, so a worktree at
`HEAD` would have had nothing to mutate. Each file was copied to `/tmp/verifier-backups/` before
mutation, mutated in place in the real tree, proved to fail, restored from the copy, and re-run
green. Because `src/web/` is untracked, `git status --porcelain` cannot see a mutation inside it,
so restoration was verified by **md5 of every touched file against its pre-mutation copy** in
addition to the porcelain comparison. Both came back identical (`PORCELAIN IDENTICAL`, 6/6 md5 OK),
and the full suite was re-run: 82 passed, 0 failed.

Five faults, five distinct assertion surfaces, none overlapping the ones the author reported.

| Mutation | Location | Narrowest covering proof | Killed |
| --- | --- | --- | --- |
| `return this.permissions().has(permission)` → `return true` (permission check always grants) | `src/web/src/app/core/session/session.store.ts:89` | `--include session.store.spec.ts --filter "hasPermission le as claims permission"` | yes - `session.store.spec.ts:15`, `expected true to be false` |
| removed the shared in-flight guard, so every caller starts its own refresh | `src/web/src/app/core/http/refresh-coordinator.ts:25-27` | `--include api.interceptor.spec.ts --filter "401 em paralelo renova uma vez"` | yes - `api.interceptor.spec.ts:160`, refresh count `3` instead of `1` |
| `status.set(totalCount === 0 ? 'empty' : 'ready')` → `status.set('ready')` (empty state never entered) | `src/web/src/app/shared/list-store.ts:28` | `--include list-state.spec.ts --filter "estado vazio"` | yes - all 4 rows failed at `list-state.spec.ts:88` |
| forbidden-screen heading `Sem permissão para esta operação` → `Acesso recusado` | `src/web/src/app/shared/screens.ts:33` | no covering proof exists, so the **whole** suite was run | **no - mutant survived: 19 files, 82 tests, 82 passed** |
| removed `reload()` from the `404` branch of `handleMutationError` | `src/web/src/app/shared/problem-details.ts:58` | `--include problem-details.spec.ts --filter "404 em mutacao recarrega a lista"` | yes - `problem-details.spec.ts:92`, `reload` called 0 times |

## Swept

`checks.md` has no rows resolving to `existing`; every row resolves to check IDs except
`observability: n/a`, which is approved policy and has nothing in the code to be wrong about. Two
rows were checked against the code and one is wrong:

- **`data lifecycle: C11 - as chaves pt.auth e pt.tenant são removidas no logout`** - false.
  `session.store.ts:79-83` (`clear()`) removes only `AUTH_STORAGE_KEY`; `pt.tenant` is deliberately
  kept, and the check's own proof asserts that it survives:
  `session.store.spec.ts:53` - `expect(localStorage.getItem(TENANT_STORAGE_KEY)).toBe('dev')`.
  The Swept row states the opposite of the behaviour its own check proves.
- `idempotency / concurrency: C7 - RefreshTokenHandler roda e revoga o token anterior` - the shared
  in-flight queue is at `refresh-coordinator.ts:25-27,37` and the mutation above confirms the
  assertion catches its removal. Row holds.

## Walk the flow with the user

Not run. This is a user-facing UI feature, so step 5 applies, but a sub-agent verifier has no
channel to the user. Logged as **not run**, not as passed.

## Ranked gaps

1. **C1 contradicts the versioned contract, and its proof asserts the contradiction.** The refresh
   token is in the `pt_refresh` cookie (`Login.cs:98`, `AuthTokenResponse` in `openapi.json`), not
   in `localStorage`. `plan.md:53` (AC 1), `plan.md:259` (door 3) and C1 all still describe the
   rejected design. The code is right and the artifacts are stale - which is the worse direction,
   because a future build reads the artifact. `.specs/features/auth-cookie-contract/plan.md:6` says
   it reopened this door; `web-frontend`'s plan and checks were never updated.
   - `src/web/src/app/features/identity/login.spec.ts:40,43`
2. **Surviving mutant: the `forbidden` screen's copy is unproven** - C10 names it verbatim and
   nothing asserts it. `src/web/src/app/shared/screens.ts:33-36`
3. **Eleven elements the plan's `Observable` decides have no check**, including two arrangement
   decisions (shell navigation order and membership; the empty-state create action on four
   screens). See the Binding sources table. `src/web/src/app/shell/shell.ts:30-54`
4. **`POST /api/v1/identity/logout` has no `Surface` row, no `Coverage` row and no check**, while
   being called on every logout. `src/web/src/app/shell/shell.ts:102`
5. **Four ordering decisions are neither implemented nor checked** - `ListQuery` has no sort field
   and no list endpoint sends one, yet `plan.md:166,175,180,184` fix an order per screen and the
   Assumptions row is marked user-confirmed. `src/web/src/app/core/api.ts:9-13`
6. **C15 / C20 / C13 / C8 / C21 / C54 each name two behaviours and prove one** - refresh `409`,
   connection-level list failure, field preservation after `429`, post-login `redirectTo`, URL
   writes on paging/search, and navigation removal when AI is disabled.
   `src/web/src/app/core/http/refresh-coordinator.ts:51`, `src/web/src/app/shared/list-state.spec.ts:107`,
   `src/web/src/app/features/identity/login.ts:145`, `src/web/src/app/features/identity/login.ts:139`,
   `src/web/src/app/features/identity/users-list.ts:197`, `src/web/src/app/shell/shell.ts:52`
7. **Every proof command in `checks.md` is unrunnable as written** (`npx vitest run ...` has no
   environment; the runner is `ng test`). A proof nobody can run is a proof nobody re-runs.
   `src/web/src/test-setup.ts:17`
8. Artifact drift, minor: `checks.md:7` says 64 checks, there are 65; `plan.md:6` says 29 routes,
   `features.json` has 30; `plan.md:219` omits `/tenants/new` from the front's URL list;
   `plan.md:169` says "skeleton" where `user-form.ts:36` renders a progress bar; the plan writes
   `/api/v1/tenants/{id}` where the contract says `{tenantId}`.

## Gate

`python3 scripts/validate_verification.py web-frontend --root /Users/luissoares/Repos/Feature.template` - exit 1, 1 error: the verdict is FAIL.

Proof totals behind the verdict: `ng test` 82 passed / 0 failed (19 files);
`playwright test` 2 passed / 0 failed; `dotnet test tests/ArchitectureTests --filter
"FullyQualifiedName~TemplateConfigTests"` 2 passed / 0 failed; `tsc --noEmit` exit 0 on both
project references.
