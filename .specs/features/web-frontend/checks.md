# Web front-end (Angular 22) - checks

Profile: ui
Plan: `.specs/features/web-frontend/plan.md`

## Intent

65 checks in 6 slices · 7 one-way doors · 0 open

## Checks

Agrupados pelas slices do plano; a numeração corre ao longo de toda a feature. Todos os comandos
correm a partir da raiz do repositório.

### S1 - Sessão, tenant e shell · 11 ficheiros novos · ~34 KB · ~9k

**C1** - Login com credenciais válidas guarda `tenantKey` e `user` em `localStorage['pt.auth']`, deixa `accessToken` e refresh token fora de qualquer storage, e navega para `/users` (WEB-01, AC 1)
Nota: a forma original desta claim (refresh token em `localStorage`) foi invalidada pela feature `auth-cookie-contract`, que moveu o token para o cookie `pt_refresh`.
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/login.spec.ts --filter "guarda a sessao e navega para users"`

**C2** - Login que devolve `401` mantém a rota `/login`, renderiza o `detail` do ProblemDetails e preserva o valor do campo email (WEB-01, AC 2)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/login.spec.ts --filter "401 mantem o email e mostra o detail"`

**C3** - Uma resposta `400` com `ValidationProblemDetails` renderiza cada mensagem de `errors[campo]` sob o campo com esse nome (WEB-01, AC 3)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/problem-details.spec.ts --filter "mapeia errors para os campos do formulario"`

**C4** - Toda a requisição para `/api/v1/**` leva o header `X-Tenant` com o valor de `localStorage['pt.tenant']` (WEB-01, AC 4)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "adiciona X-Tenant"`

**C5** - Com `accessToken` em memória, toda a requisição para `/api/v1/**` leva `Authorization: Bearer <accessToken>` (WEB-01, AC 5)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "adiciona Authorization Bearer"`

**C6** - Um `401` numa requisição autenticada dispara exatamente um `POST /api/v1/identity/refresh`, repete a requisição original com o token novo, e um `401` no refresh limpa a sessão e navega para `/login` (WEB-01, AC 6)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "401 renova e repete"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "refresh 401 limpa a sessao"`

**C7** - Três requisições que recebem `401` em paralelo produzem uma única chamada a `/api/v1/identity/refresh` e as três são repetidas com o mesmo token (WEB-01, AC 7)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "401 em paralelo renova uma vez"`

**C8** - Navegar para uma rota protegida sem sessão redireciona para `/login?redirectTo=<rota>` e, após login, navega para essa rota (WEB-01, AC 8)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/guards/guards.spec.ts --filter "redireciona com redirectTo e volta"`

**C9** - `hasPermission('identity.user.manage')` devolve `true` quando o payload do access token traz essa claim `permission` e `false` quando não traz (WEB-01, AC 9)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/session/session.store.spec.ts --filter "hasPermission le as claims permission"`

**C10** - Uma resposta `403` renderiza o ecrã `forbidden` com a mensagem "Sem permissão para esta operação" e um botão que volta à rota anterior (WEB-01, AC 10)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "403 abre o ecra forbidden"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/screens.spec.ts --filter "mostra a mensagem de sem permissao"`

**C11** - Sair remove a chave `pt.auth` de `localStorage`, esvazia o `accessToken` em memória e navega para `/login` (WEB-01, AC 11)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/session/session.store.spec.ts --filter "logout limpa pt.auth e o token em memoria"`

**C12** - Com sessão ativa, a barra superior mostra o `firstName` do utilizador e o `tenantKey` corrente (WEB-01, AC 12)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "mostra firstName e tenantKey"`

**C13** - Login que devolve `429` mostra "Demasiadas tentativas, tente dentro de um minuto" e mantém os campos preenchidos (WEB-01, AC 2)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/login.spec.ts --filter "429 mostra limite de tentativas"`

**C14** - Refresh que devolve `404` limpa a sessão e navega para `/login` (WEB-01, AC 6)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "refresh 404 limpa a sessao"`

**C15** - Login ou refresh que devolve `409` mostra "Tenant inválido" junto ao campo tenant e não guarda sessão (WEB-01, AC 2)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/login.spec.ts --filter "409 mostra tenant invalido"`

**C16** - Refresh que devolve `400` limpa a sessão e navega para `/login` (WEB-01, AC 6)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/http/api.interceptor.spec.ts --filter "refresh 400 limpa a sessao"`

### S2 - Utilizadores · 9 ficheiros novos · ~30 KB · ~8k

**C17** - Abrir `/users` emite `GET /api/v1/identity/users?pageNumber=1&pageSize=20` e renderiza as colunas email, nome, `createdAt` e `lastLoginAt` (WEB-02, AC 13)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/users-list.spec.ts --filter "carrega a primeira pagina com as quatro colunas"`

**C18** - Enquanto a lista carrega, o ecrã mostra `mat-progress-bar` e o paginador fica desativado, nos 4 ecrãs de lista (WEB-02, AC 14)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de carregamento"`

**C19** - `totalCount` igual a `0` mostra o estado vazio com a ação de criar, nos 4 ecrãs de lista (WEB-02, AC 15)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado vazio"`

**C20** - Uma lista que falha com `500` ou com erro de ligação mostra o `title` do ProblemDetails e o botão "Tentar de novo" repete a mesma query, nos 4 ecrãs de lista (WEB-02, AC 16)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de erro repete a query"`

**C21** - Mudar de página ou pesquisar escreve `pageNumber`, `pageSize` e `searchTerm` nos query params do URL e a lista recarrega a partir deles (WEB-02, AC 17)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/users-list.spec.ts --filter "sincroniza query params"`

**C22** - Submeter o formulário de criação emite `POST /api/v1/identity/register` e, com `201`, navega para `/users` e mostra o snackbar "Utilizador criado" (WEB-02, AC 18)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/user-form.spec.ts --filter "201 navega e mostra o snackbar"`

**C23** - Confirmar a eliminação emite `DELETE /api/v1/identity/users/{userId}` e, com `204`, a linha desaparece da tabela (WEB-02, AC 19)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/users-list.spec.ts --filter "204 remove a linha"`

**C24** - Fechar o diálogo de eliminação sem confirmar não emite nenhuma requisição HTTP (WEB-02, AC 20)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/users-list.spec.ts --filter "cancelar nao emite requisicao"`

**C25** - Sem a permissão de gestão e sem a role `Admin`, as ações de gestão não são renderizadas, nas 5 permissões de gestão (WEB-02, AC 21)
Proof: `cd src/web && npx ng test --no-watch --include src/app/core/session/permission.directive.spec.ts --filter "esconde acoes sem permissao"`

**C26** - Abrir `/users/{userId}` emite `GET /api/v1/identity/users/{userId}` e `GET /api/v1/identity/users/{userId}/roles` e mostra os dois resultados no mesmo ecrã (WEB-02, AC 22)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/user-detail.spec.ts --filter "carrega utilizador e roles"`

**C27** - Guardar a edição emite `PUT /api/v1/identity/users/{userId}` e, com `200`, os campos em ecrã passam a mostrar os valores da resposta (WEB-02, AC 23)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/user-form.spec.ts --filter "200 substitui os dados em ecra"`

**C28** - Um `404` num GET de detalhe renderiza o ecrã not-found com o `title` do ProblemDetails (WEB-02, AC 16)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/problem-details.spec.ts --filter "404 renderiza not found"`

**C29** - `POST /api/v1/identity/register` que devolve `409` mostra o `detail` junto ao campo email e mantém o formulário preenchido (WEB-02, AC 18)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/user-form.spec.ts --filter "409 marca o campo email"`

**C30** - Um `404` num `PUT` ou `DELETE` mostra o snackbar "Registo não encontrado" e recarrega a lista (WEB-02, AC 19)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/problem-details.spec.ts --filter "404 em mutacao recarrega a lista"`

**C31** - Um `403` em `GET /api/v1/identity/users/{userId}/roles` renderiza o bloco "Sem acesso aos roles" e mantém o resto do ecrã de detalhe renderizado (WEB-02, AC 22)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/identity/user-detail.spec.ts --filter "403 nos roles mantem o ecra"`

### S3 - Roles e permissões · 8 ficheiros novos · ~28 KB · ~7k

**C32** - Abrir `/roles` emite `GET /api/v1/authorization/roles?pageNumber=1&pageSize=20` e renderiza nome e descrição por linha (WEB-03, AC 24)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/roles-list.spec.ts --filter "carrega a primeira pagina"`

**C33** - Abrir `/roles/{roleId}` emite `GET /api/v1/authorization/roles/{roleId}` e mostra nome e descrição do role (WEB-03, AC 25)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/role-detail.spec.ts --filter "carrega o role"`

**C34** - O ecrã de detalhe emite `GET /api/v1/authorization/roles/{roleId}/permissions` e lista as permissões atribuídas por `name` (WEB-03, AC 25)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/role-detail.spec.ts --filter "lista as permissoes do role"`

**C35** - Atribuir uma permissão emite `POST /api/v1/authorization/roles/{roleId}/permissions` e, com `204`, a permissão aparece na lista sem nova chamada de leitura (WEB-03, AC 26)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/role-detail.spec.ts --filter "204 acrescenta a permissao"`

**C36** - Revogar uma permissão emite `DELETE /api/v1/authorization/roles/{roleId}/permissions/{permissionId}` e, com `204`, a permissão sai da lista (WEB-03, AC 27)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/role-detail.spec.ts --filter "204 remove a permissao"`

**C37** - Criar role emite `POST /api/v1/authorization/roles` e, com `201`, o role aparece na lista e o diálogo fecha (WEB-03, AC 24)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/roles-list.spec.ts --filter "201 acrescenta o role"`

**C38** - Criar role que devolve `409` mantém o diálogo aberto e mostra o `detail` junto ao campo Nome (WEB-03, AC 28)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/roles-list.spec.ts --filter "409 mantem o dialogo aberto"`

**C39** - Guardar a edição de um role emite `PUT /api/v1/authorization/roles/{roleId}` e, com `200`, a linha na lista passa a mostrar os valores da resposta (WEB-03, AC 29)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/roles-list.spec.ts --filter "200 atualiza a linha"`

**C40** - Confirmar a eliminação de um role emite `DELETE /api/v1/authorization/roles/{roleId}` e, com `204`, a linha desaparece (WEB-03, AC 30)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/roles-list.spec.ts --filter "204 remove o role"`

**C41** - Atribuir e revogar um role a um utilizador emitem `POST /api/v1/authorization/users/{userId}/roles` e `DELETE /api/v1/authorization/users/{userId}/roles/{roleId}`, e cada um é seguido de `GET /api/v1/authorization/users/{userId}/roles` (WEB-03, AC 31)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/user-roles.spec.ts --filter "atribui e revoga recarregando"`

**C42** - Abrir `/permissions` emite `GET /api/v1/authorization/permissions` e renderiza nome e descrição por linha (WEB-03, AC 32)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/permissions-list.spec.ts --filter "carrega a primeira pagina"`

**C43** - Criar permissão emite `POST /api/v1/authorization/permissions` e, com `201`, a permissão aparece na lista (WEB-03, AC 32)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/permissions-list.spec.ts --filter "201 acrescenta a permissao"`

**C44** - Criar permissão que devolve `409` mantém o diálogo aberto e mostra o `detail` junto ao campo Nome (WEB-03, AC 32)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/permissions-list.spec.ts --filter "409 mantem o dialogo aberto"`

**C45** - Guardar a edição de uma permissão emite `PUT /api/v1/authorization/permissions/{permissionId}` e, com `200`, a linha passa a mostrar os valores da resposta (WEB-03, AC 32)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/permissions-list.spec.ts --filter "200 atualiza a linha"`

**C46** - Confirmar a eliminação de uma permissão emite `DELETE /api/v1/authorization/permissions/{permissionId}` e, com `204`, a linha desaparece (WEB-03, AC 32)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/authorization/permissions-list.spec.ts --filter "204 remove a permissao"`

### S4 - Tenants · 4 ficheiros novos · ~14 KB · ~4k

**C47** - Abrir `/tenants` emite `GET /api/v1/tenants?pageNumber=1&pageSize=20` e mostra `tenantKey`, `displayName`, `isActive` e `isolationMode` (WEB-04, AC 33)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenants-list.spec.ts --filter "carrega a primeira pagina com as quatro colunas"`

**C48** - Criar tenant emite `POST /api/v1/tenants` e, com `201`, navega para `/tenants` (WEB-04, AC 34)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenant-form.spec.ts --filter "201 navega para tenants"`

**C49** - Criar tenant que devolve `409` mantém o formulário preenchido e mostra o `detail` junto ao campo Chave (WEB-04, AC 35)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenant-form.spec.ts --filter "409 marca o campo chave"`

**C50** - Guardar a edição emite `PUT /api/v1/tenants/{id}` e, com `200`, os campos em ecrã passam a mostrar os valores da resposta (WEB-04, AC 36)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenant-form.spec.ts --filter "200 substitui os dados em ecra"`

**C51** - `PUT /api/v1/tenants/{id}` que devolve `400` mostra cada mensagem de `errors[campo]` sob o campo correspondente (WEB-04, AC 36)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenant-form.spec.ts --filter "400 marca os campos"`

**C52** - Confirmar a desativação emite `DELETE /api/v1/tenants/{id}` e, com `204`, a linha passa a mostrar `isActive` falso (WEB-04, AC 37)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenants-list.spec.ts --filter "204 marca a linha como inativa"`

**C53** - Abrir `/tenants/{id}` emite `GET /api/v1/tenants/{id}` e mostra os sete campos de `TenantOutput` (WEB-04, AC 38)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/tenants/tenant-form.spec.ts --filter "mostra os sete campos"`

### S5 - Chat AI · 2 ficheiros novos · ~7 KB · ~2k

**C54** - `POST /api/v1/ai/chat` que devolve `404` com o título "Feature disabled" remove o item AI da navegação durante a sessão corrente (WEB-05, AC 39)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "404 esconde o item AI"`

**C55** - Enviar uma mensagem acrescenta-a ao histórico local, emite `POST /api/v1/ai/chat` com `message` e `history`, e renderiza o `reply` da resposta (WEB-05, AC 40)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "envia e renderiza o reply"`

**C56** - Enquanto a resposta está pendente, o botão Enviar está desativado e o indicador de escrita é visível (WEB-05, AC 41)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "pendente desativa o envio"`

**C57** - Um `401` no chat passa pelo caminho de refresh do interceptor antes de qualquer mensagem de erro ser mostrada (WEB-05, AC 42)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "401 renova antes de falhar"`

### S6 - Harness e portões · 12 ficheiros novos · ~24 KB · ~6k

**C58** - Uma rota presente em `features.json` sem nenhuma referência sob `src/web/src/app/**` faz `npm test` sair com código diferente de zero (WEB-06, AC 43)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "todas as rotas de features.json tem cliente"`

**C59** - Uma pasta com nome `services`, `components`, `models`, `pages`, `dtos` ou `interfaces` sob `src/web/src/app/features/` faz `npm test` sair com código diferente de zero (WEB-06, AC 44)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "sem pastas de camada"`

**C60** - `npm run e2e` executa login, listagem, criação e eliminação de utilizador contra `http://localhost:5080` com o tenant `dev` e sai com código zero (WEB-06, AC 45)
Proof: `cd src/web && npx playwright test e2e/users.spec.ts -g "cria e elimina um utilizador"`

**C61** - O job `web-e2e` de `.github/workflows/ci.yml` declara o passo que publica `playwright-report` como artefacto com `if: always()` (WEB-06, AC 46)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "web-e2e publica o relatorio"`

**C62** - `src/web/tsconfig.json` declara `strict: true` e `npx tsc --noEmit` sai com código zero (WEB-06, AC 47)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "tsconfig strict"`

**C63** - `.template.config/template.json` lista `**/node_modules/**` e `**/dist/**` no `exclude` do modificador de sources (WEB-06, AC 47)
Proof: `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~TemplateConfigTests"`

**C64** - Os providers HTTP (interceptor de tenant e de autenticação) estão registados nos três sítios que montam a aplicação: `app.config.ts`, o setup do Vitest e o build servido ao Playwright (WEB-06, AC 43)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "providers http em todas as montagens"`

**C65** - `npx playwright test e2e/auth.spec.ts` prova, contra a API real, que uma sessão sem access token em memória (o estado a seguir a recarregar a página, igual ao de um token expirado) é renovada a partir do refresh token sem devolver o utilizador ao login (WEB-01, AC 6)
Proof: `cd src/web && npx playwright test e2e/auth.spec.ts -g "renova o token expirado"`

## Coverage

Uma set row por rota da secção `Surface` do plano; os membros são os statuses. `401` e `403` juntam-se aos checks do interceptor porque o interceptor é agnóstico da rota: a decisão está num único ficheiro e é aí que é asserida — as rotas individuais não a reimplementam, e o teste de cobertura de rotas (C58) é o que impede uma rota de existir sem passar por ele.

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/v1/identity/register` statuses (3) | 201 C22 · 400 C3 · 409 C29 | - |
| `POST /api/v1/identity/login` statuses (5) | 200 C1 · 400 C3 · 401 C2 · 409 C15 · 429 C13 | - |
| `POST /api/v1/identity/refresh` statuses (6) | 200 C6 · 400 C16 · 401 C6 · 404 C14 · 409 C15 · 429 C13 | - |
| `GET /api/v1/identity/users` statuses (3) | 200 C17 · 401 C6 · 403 C10 | - |
| `GET /api/v1/identity/users/{userId}` statuses (4) | 200 C26 · 401 C6 · 403 C10 · 404 C28 | - |
| `PUT /api/v1/identity/users/{userId}` statuses (4) | 200 C27 · 401 C6 · 403 C10 · 404 C30 | - |
| `DELETE /api/v1/identity/users/{userId}` statuses (4) | 204 C23 · 401 C6 · 403 C10 · 404 C30 | - |
| `GET /api/v1/identity/users/{userId}/roles` statuses (3) | 200 C26 · 401 C6 · 403 C31 | - |
| `GET /api/v1/authorization/roles` statuses (3) | 200 C32 · 401 C6 · 403 C10 | - |
| `POST /api/v1/authorization/roles` statuses (5) | 201 C37 · 400 C3 · 401 C6 · 403 C10 · 409 C38 | - |
| `GET /api/v1/authorization/roles/{roleId}` statuses (4) | 200 C33 · 401 C6 · 403 C10 · 404 C28 | - |
| `PUT /api/v1/authorization/roles/{roleId}` statuses (4) | 200 C39 · 401 C6 · 403 C10 · 404 C30 | - |
| `DELETE /api/v1/authorization/roles/{roleId}` statuses (4) | 204 C40 · 401 C6 · 403 C10 · 404 C30 | - |
| `GET /api/v1/authorization/roles/{roleId}/permissions` statuses (4) | 200 C34 · 401 C6 · 403 C10 · 404 C28 | - |
| `POST /api/v1/authorization/roles/{roleId}/permissions` statuses (4) | 204 C35 · 401 C6 · 403 C10 · 404 C30 | - |
| `DELETE /api/v1/authorization/roles/{roleId}/permissions/{permissionId}` statuses (4) | 204 C36 · 401 C6 · 403 C10 · 404 C30 | - |
| `GET /api/v1/authorization/permissions` statuses (3) | 200 C42 · 401 C6 · 403 C10 | - |
| `POST /api/v1/authorization/permissions` statuses (5) | 201 C43 · 400 C3 · 401 C6 · 403 C10 · 409 C44 | - |
| `PUT /api/v1/authorization/permissions/{permissionId}` statuses (4) | 200 C45 · 401 C6 · 403 C10 · 404 C30 | - |
| `DELETE /api/v1/authorization/permissions/{permissionId}` statuses (4) | 204 C46 · 401 C6 · 403 C10 · 404 C30 | - |
| `GET /api/v1/authorization/users/{userId}/roles` statuses (3) | 200 C41 · 401 C6 · 403 C10 | - |
| `POST /api/v1/authorization/users/{userId}/roles` statuses (4) | 204 C41 · 401 C6 · 403 C10 · 404 C30 | - |
| `DELETE /api/v1/authorization/users/{userId}/roles/{roleId}` statuses (4) | 204 C41 · 401 C6 · 403 C10 · 404 C30 | - |
| `GET /api/v1/tenants` statuses (3) | 200 C47 · 401 C6 · 403 C10 | - |
| `POST /api/v1/tenants` statuses (5) | 201 C48 · 400 C3 · 401 C6 · 403 C10 · 409 C49 | - |
| `GET /api/v1/tenants/{id}` statuses (4) | 200 C53 · 401 C6 · 403 C10 · 404 C28 | - |
| `PUT /api/v1/tenants/{id}` statuses (5) | 200 C50 · 400 C51 · 401 C6 · 403 C10 · 404 C30 | - |
| `DELETE /api/v1/tenants/{id}` statuses (4) | 204 C52 · 401 C6 · 403 C10 · 404 C30 | - |
| `POST /api/v1/ai/chat` statuses (3) | 200 C55 · 401 C57 · 404 C54 | - |
| ecrãs do plano (14) | login C1 · shell C12 · users-list C17 · user-form C22 · user-detail C26 · user-roles C41 · roles-list C32 · role-detail C33 · permissions-list C42 · tenants-list C47 · tenant-form C48 · ai-chat C55 · forbidden C10 · not-found C28 | - |
| estados dos 4 ecrãs de lista (12) | C18, C19 e C20, cada um table-driven sobre os 4 ecrãs de lista - 12 combinações | - |
| one-way doors do plano (7) | VSA no front C59 · interceptor único C4 · sessão no browser C11 · permissões do JWT C9 · cliente à mão C58 · tenant por chave C4 · exclude do template C63 | - |
| permissões que escondem ações (5) | C25, table-driven sobre `identity.user.manage`, `authorization.role.manage`, `authorization.permission.manage`, `tenants.manage` e a role `Admin` | - |
| estados do chat AI (3) | vazio C55 · pendente C56 · indisponível C54 | - |
| bootstrap: providers HTTP (3 montagens) | `app.config.ts` C5 · setup do Vitest C4 · build servido ao Playwright C64 | - |
| renovação de token contra a API real (2) | interceptor C7 · ponta a ponta C65 | - |

- Claims que nomeiam status code, rota ou forma de resposta: C1, C2, C6, C10, C13, C14, C15, C16, C22, C28, C29, C30, C31, C35, C36, C38, C40, C44, C46, C49, C51, C52, C54, C57 - cada um tem uma prova que atravessa a fronteira HTTP (MSW ao nível do componente, API real no E2E)
- Nenhum outro check reclama mais do que o único caso que a sua prova exercita

## Test policy

O repositório responde onde os testes vivem (`docs/architecture/guidelines.md`, `AGENTS.md`) e como
se correm (`make verify`), mas não responde a nenhuma das duas perguntas para código de front-end -
qual nível prova que código, e quanto do espaço de entrada uma prova tem de asserir. Não existe
front-end no repositório, logo também não há precedente. Estas linhas são a barra sob a qual este
build corre.

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide, atravessado por uma fronteira | uma na fronteira **e** uma no seu próprio nível | o contrato na fronteira; um caso asserido por linha da tabela de decisão no seu próprio nível |
| Decide, não atravessado por uma fronteira | uma no seu próprio nível | um caso asserido por linha da tabela de decisão |
| Ponto de entrada que não decide | uma na fronteira | entrada aceite, cada entrada rejeitada, cada caminho de erro |
| Instrumentação, pass-through | nenhuma própria | coberto pela prova do consumidor |

Evidence:

- `src/app/core/http/api.interceptor.ts`: decide sobre `401`, `403` e sobre a fila de refresh; 4 pontos de ramificação -> decide, atravessado pela fronteira HTTP
- `src/app/core/session/session.store.ts`: descodifica o payload, decide `hasPermission` por claim; 3 pontos de ramificação -> decide
- `src/app/shared/problem-details.ts`: mapeia `400`, `404` e o caso genérico para estados de ecrã; 3 pontos de ramificação -> decide
- `src/app/features/**/*.client.ts` (um por slice): monta URL e devolve a resposta tipada, sem condicional -> instrumentação, coberta pela prova do componente
- análogo mais próximo no repositório por forma de código: `src/Api/Host/Extensions/EndpointExtensions.cs` (`FeatureGateFilter`, um ponto de decisão) já é provado no seu próprio nível em `tests/Api.Tests/Ai/ChatAiTests.cs` - o interceptor tem a mesma forma e recebe o mesmo tratamento

Cost: 3 provas no próprio nível em 3 ficheiros (`api.interceptor.spec.ts`, `session.store.spec.ts`,
`problem-details.spec.ts`). Sem estas linhas, as três tabelas de decisão ficam provadas apenas por
um caminho de componente que por acaso as atravessa, e uma segunda ramificação errada passa verde.

## Swept

- validation: C3, C51 - `ValidationProblemDetails` mapeado campo a campo, no login e no formulário de tenant
- failure modes: C20, C30 - lista que falha com `500` ou ligação recusada, e `404` a meio de uma mutação
- idempotency: C7 - três `401` em paralelo produzem um único refresh, porque `RefreshTokenHandler` roda e revoga o token anterior a cada chamada
- authorization: C9, C10, C25 - permissões derivadas do token, `403` no interceptor, ações escondidas por permissão
- concurrency: C7 - a fila partilhada de refresh é a única secção com corrida no front
- data lifecycle: C11 - as chaves `pt.auth` e `pt.tenant` são removidas no logout; não há outro dado persistido no browser
- dependency failure: C20, C54 - API a devolver `500` ou ligação recusada cai no estado de erro; feature flag desligada devolve `404` e o item some da navegação
- state transitions: C6, C8, C11 - anónimo -> autenticado -> expirado-renovado -> anónimo, com o guard a cobrir a entrada pela porta errada
- observability: n/a - nenhum critério do plano pede telemetria no browser; a instrumentação fica na API (`ObservabilityConfiguration`), e este trabalho não acrescenta sink nem sampler do lado do cliente

## Handoff

Aritmética antes de qualquer código, com a estimativa de `wc -c` dos ficheiros que cada slice
escreve, dividida por quatro:

- S1 ~9k + S2 ~8k + S3 ~7k + S4 ~4k + S5 ~2k + S6 ~6k = ~36k
- contexto de leitura já fixo (contratos da API, `features.json`, RBAC matrix, configuração do workspace): ~20k
- total ~56k, abaixo do orçamento de 150k -> **um único builder**, sem handoff
