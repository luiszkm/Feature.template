# Web front-end (Angular 22)

Sources:

- conversa - stack Angular Material, profile `ui`, tenant por campo no login, harness com API real
- `features.json` - o índice das 29 rotas, policy e feature flag por slice
- `docs/security/RBAC_MATRIX.md` - as 11 policies e os nomes canónicos de permissão
- `docs/architecture/vsa.md` · `.cursor/rules/architecture-vsa.mdc` - a convenção de slice plano que o front espelha
- nenhuma fonte de design ou de copy existe - a copy é decidida nos checks, não copiada de um ficheiro

## Problem

A API expõe 29 endpoints e a única forma de os usar é Scalar em Development ou `curl`. Qualquer demonstração do template pára na fronteira HTTP: `docs/guides/getting-started.md` manda o utilizador abrir `/scalar/v1`, e o repositório não tem um único ficheiro de front-end.

O custo é pago por quem gera um produto novo com `dotnet new product-template`: recebe autenticação JWT com refresh rotation, multi-tenancy por header e RBAC por claims, e tem de reimplementar do zero a ligação disso a um ecrã — interceptor de tenant, fila de refresh em `401`, guards por permissão. O template promete ponto de partida e entrega metade dele.

Quando isto estiver entregue, `npm start` em `src/web` dá um ecrã de login que resolve tenant, sessão que se renova sozinha, e CRUD navegável sobre utilizadores, roles, permissões, tenants e chat AI — sobre as mesmas rotas que `features.json` já indexa.

## Out of scope

| Excluded | Why |
| --- | --- |
| SSR / hydration / PWA | nada no pedido depende disso e cada um traz o seu próprio conjunto de doors |
| i18n / multi-idioma | a API não expõe idioma e a copy ainda não existe em nenhuma fonte |
| Recuperação de password, confirmação de email | não há rota na API — foram removidas em `b55602e` e `af026c8` |
| Streaming token-a-token no chat AI | `POST /api/v1/ai/chat` devolve a resposta completa; streaming exigia mudar a API |
| Tema visual próprio para além dos tokens M3 por omissão | sem fonte de design; inventar identidade visual aqui não é revisível |
| Resolução de tenant por subdomínio no front | escolhido campo no login; o subdomínio continua a funcionar server-side sem front |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Onde vive o workspace | `src/web/`, workspace npm próprio, fora do `.sln` | mantém `dotnet build` sem dependência de Node; `src/Api` fica intacto | n |
| Versão Angular | `22.1.x` com preset zoneless + standalone | é a `latest` no registry hoje (`@angular/core@22.1.6`) e traz Vitest por omissão | y |
| Formulários | Signal Forms (estáveis em v22) em vez de Reactive Forms | evita instalar o padrão antigo num template novo que vai ser copiado | n |
| Estado | signals nativos + `resource()`; sem NgRx | nenhuma das telas partilha estado entre módulos; NgRx SignalStore é acrescentável depois sem reescrever | n |
| Cliente HTTP tipado | escrito à mão, um ficheiro por slice | o documento OpenAPI só é mapeado em Development (`OpenApiHostConfiguration` sai cedo fora de dev), logo codegen exigiria servidor dev em CI | n |
| Dev server | `ng serve` em `:4200` com `proxy.conf.json` a apontar para a API | evita mexer em `Cors:AllowedOrigins` (que hoje lista 3000/5173/5080, não 4200) | n |
| Produção | imagem nginx separada a servir `dist/` e a fazer proxy de `/api` | espelha o `docker-compose.yml` atual, que já separa `api` de infra | n |
| Tenant por omissão no campo de login | `dev` | é o tenant semeado e documentado em `getting-started.md` | y |
| Idioma da UI | português de Portugal, igual aos docs do repo | os docs e o `AGENTS.md` estão em pt-PT | n |
| Utilizador delegou o resto das escolhas visuais | densidade compacta nas tabelas, ordenação por `createdAt` desc | user delegated | y |

**Open questions:** none - all resolved or logged above.

## Criteria

### S1: Sessão, tenant e shell (P1)

**Acceptance Criteria**

1. WHEN o utilizador submete o formulário de login com tenant, email e password válidos THEN o sistema SHALL guardar o `accessToken` em memória, `refreshToken` e `tenantKey` em `localStorage`, e navegar para `/users`
2. IF `POST /api/v1/identity/login` devolver `401` THEN o sistema SHALL manter o utilizador em `/login`, mostrar o `detail` do ProblemDetails e preservar o valor do campo email
3. IF a resposta for `400` com `ValidationProblemDetails` THEN o sistema SHALL mostrar cada mensagem de `errors[campo]` sob o campo correspondente do formulário
4. The system SHALL enviar o header `X-Tenant: <tenantKey>` em todas as requisições para `/api/v1/**`
5. WHILE existir `accessToken` em memória the system SHALL enviar `Authorization: Bearer <accessToken>` em todas as requisições para `/api/v1/**`
6. IF uma requisição autenticada devolver `401` THEN o sistema SHALL chamar `POST /api/v1/identity/refresh` uma única vez, repetir a requisição original com o token novo, e — se o refresh devolver `401` — limpar a sessão e navegar para `/login`
7. WHILE duas ou mais requisições recebem `401` em paralelo the system SHALL emitir exatamente uma chamada a `/api/v1/identity/refresh` e servir o mesmo token às restantes
8. WHEN um utilizador não autenticado abre uma rota protegida THEN o sistema SHALL navegar para `/login?redirectTo=<rota>` e, após login com sucesso, navegar para essa rota
9. The system SHALL derivar o conjunto de permissões do payload do access token (claim `permission`) e expor `hasPermission(name)`, sem validar assinatura no browser
10. IF uma requisição devolver `403` THEN o sistema SHALL renderizar o ecrã `forbidden` com a mensagem "Sem permissão para esta operação" e um botão que volta à rota anterior
11. WHEN o utilizador clica em Sair THEN o sistema SHALL limpar `localStorage['pt.auth']` e o token em memória e navegar para `/login`
12. WHILE a sessão está ativa the system SHALL mostrar na barra superior o `firstName` do utilizador e o `tenantKey` corrente

**Independent test:** login com `admin@producttemplate.com` / tenant `dev`, forçar expiração do access token e confirmar que a próxima chamada renova e sucede sem voltar ao login.

### S2: Utilizadores (P1)

**Acceptance Criteria**

13. WHEN `/users` abre THEN o sistema SHALL chamar `GET /api/v1/identity/users?pageNumber=1&pageSize=20` e renderizar uma tabela com email, nome, `createdAt` e `lastLoginAt`
14. WHILE a lista está a carregar the system SHALL mostrar `mat-progress-bar` e manter os controlos de paginação desativados
15. IF `totalCount` for `0` THEN o sistema SHALL mostrar o estado vazio "Nenhum utilizador" com a ação "Criar utilizador"
16. IF a chamada de lista devolver `500` THEN o sistema SHALL mostrar o estado de erro com o `title` do ProblemDetails e um botão "Tentar de novo" que repete a mesma query
17. WHEN o utilizador muda de página ou pesquisa THEN o sistema SHALL refletir `pageNumber`, `pageSize` e `searchTerm` nos query params do URL e recarregar a lista a partir deles
18. WHEN o utilizador submete o formulário de criação THEN o sistema SHALL chamar `POST /api/v1/identity/register` e, com `201`, navegar para `/users` e mostrar o snackbar "Utilizador criado"
19. WHEN o utilizador confirma a eliminação no diálogo THEN o sistema SHALL chamar `DELETE /api/v1/identity/users/{userId}` e remover a linha da tabela ao receber `204`
20. IF o utilizador fecha o diálogo de eliminação sem confirmar THEN o sistema SHALL não emitir nenhuma requisição HTTP
21. WHERE o utilizador não tem a permissão `identity.user.manage` nem a role `Admin` the system SHALL ocultar as ações Editar e Eliminar da tabela
22. WHEN o utilizador abre `/users/{userId}` THEN o sistema SHALL chamar `GET /api/v1/identity/users/{userId}` e `GET /api/v1/identity/users/{userId}/roles` e mostrar ambos no mesmo ecrã
23. WHEN o utilizador guarda a edição THEN o sistema SHALL chamar `PUT /api/v1/identity/users/{userId}` e, com `200`, substituir os dados em ecrã pela resposta

**Independent test:** criar utilizador, encontrá-lo pela pesquisa, editar o nome, eliminá-lo — tudo pela UI, com tenant `dev`.

### S3: Roles e permissões (P1)

**Acceptance Criteria**

24. WHEN `/roles` abre THEN o sistema SHALL chamar `GET /api/v1/authorization/roles?pageNumber=1&pageSize=20` e renderizar nome e descrição por linha
25. WHEN o utilizador abre `/roles/{roleId}` THEN o sistema SHALL chamar `GET /api/v1/authorization/roles/{roleId}` e `GET /api/v1/authorization/roles/{roleId}/permissions` e listar as permissões atribuídas por nome
26. WHEN o utilizador atribui uma permissão a um role THEN o sistema SHALL chamar `POST /api/v1/authorization/roles/{roleId}/permissions` e, com `204`, acrescentar a permissão à lista sem recarregar a página
27. WHEN o utilizador revoga uma permissão THEN o sistema SHALL chamar `DELETE /api/v1/authorization/roles/{roleId}/permissions/{permissionId}` e remover a permissão da lista ao receber `204`
28. IF criar role devolver `409` THEN o sistema SHALL manter o diálogo aberto e mostrar o `detail` do ProblemDetails junto ao campo Nome
29. WHEN o utilizador guarda a edição de um role THEN o sistema SHALL chamar `PUT /api/v1/authorization/roles/{roleId}` e, com `200`, atualizar a linha na lista
30. WHEN o utilizador confirma a eliminação de um role THEN o sistema SHALL chamar `DELETE /api/v1/authorization/roles/{roleId}` e remover a linha ao receber `204`
31. WHEN o utilizador atribui ou revoga um role a um utilizador em `/users/{userId}/roles` THEN o sistema SHALL chamar `POST /api/v1/authorization/users/{userId}/roles` ou `DELETE /api/v1/authorization/users/{userId}/roles/{roleId}` e recarregar `GET /api/v1/authorization/users/{userId}/roles`
32. The system SHALL mapear os botões Criar, Editar e Eliminar de `/permissions` para `POST`, `PUT` e `DELETE /api/v1/authorization/permissions[/{permissionId}]`, e a lista para `GET /api/v1/authorization/permissions`

**Independent test:** criar o role `Auditor`, atribuir-lhe `identity.user.read`, atribuir o role ao utilizador seed e confirmar que `/users` deixa de esconder a lista para esse utilizador.

### S4: Tenants (P2)

**Acceptance Criteria**

33. WHEN `/tenants` abre THEN o sistema SHALL chamar `GET /api/v1/tenants?pageNumber=1&pageSize=20` e mostrar `tenantKey`, `displayName`, `isActive` e `isolationMode`
34. WHEN o utilizador submete o formulário de criação THEN o sistema SHALL chamar `POST /api/v1/tenants` e, com `201`, navegar para `/tenants`
35. IF criar tenant devolver `409` THEN o sistema SHALL manter o formulário preenchido e mostrar o `detail` junto ao campo Chave
36. WHEN o utilizador guarda a edição THEN o sistema SHALL chamar `PUT /api/v1/tenants/{id}` e, com `200`, atualizar a linha na lista
37. WHEN o utilizador confirma a desativação THEN o sistema SHALL chamar `DELETE /api/v1/tenants/{id}` e marcar a linha como inativa ao receber `204`
38. WHEN o utilizador abre `/tenants/{id}` THEN o sistema SHALL chamar `GET /api/v1/tenants/{id}` e mostrar os sete campos de `TenantOutput`

**Independent test:** criar tenant `qa`, editar o nome, desativá-lo e confirmar a linha marcada como inativa.

### S5: Chat AI (P3)

**Acceptance Criteria**

39. WHERE `POST /api/v1/ai/chat` devolve `404` com o título "Feature disabled" the system SHALL ocultar o item AI da navegação durante a sessão corrente
40. WHEN o utilizador envia uma mensagem THEN o sistema SHALL acrescentar a mensagem ao histórico local, chamar `POST /api/v1/ai/chat` com `message` e `history`, e renderizar o `reply` da resposta
41. WHILE a resposta do chat está pendente the system SHALL manter o botão Enviar desativado e mostrar o indicador de escrita
42. IF o chat devolver `401` THEN o sistema SHALL seguir o mesmo caminho de refresh do critério 6 antes de mostrar erro ao utilizador

**Independent test:** com `FeatureFlags:EnableAI=true`, enviar "olá" e ver a resposta do `StubLlmService`; com a flag a `false`, confirmar que o item de navegação não aparece.

### S6: Harness e portões (P1)

**Acceptance Criteria**

43. The system SHALL falhar `npm test` quando qualquer rota listada em `features.json` não for referenciada por nenhum cliente sob `src/web/src/app/features/**`
44. The system SHALL falhar `npm test` quando existir uma pasta com nome `services`, `components`, `models`, `pages`, `dtos` ou `interfaces` sob `src/web/src/app/features/`
45. WHEN `npm run e2e` corre THEN o sistema SHALL executar login → listar utilizadores → criar → eliminar contra a API real em `http://localhost:5080` com o tenant `dev`
46. IF o job `web-e2e` do CI falhar THEN o sistema SHALL publicar o relatório Playwright como artefacto do workflow
47. The system SHALL falhar `npm run build` quando o TypeScript compilar com `strict` desativado ou com erro de tipo em qualquer ficheiro de `src/web/src`

**Independent test:** apagar a chamada a `DELETE /api/v1/tenants/{id}` do cliente de tenants e confirmar que `npm test` falha no teste de cobertura de rotas.

## Traceability

| ID | Slice | Criteria | Status |
| --- | --- | --- | --- |
| WEB-01 | S1 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 | Pending |
| WEB-02 | S2 | 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 | Pending |
| WEB-03 | S3 | 24, 25, 26, 27, 28, 29, 30, 31, 32 | Pending |
| WEB-04 | S4 | 33, 34, 35, 36, 37, 38 | Pending |
| WEB-05 | S5 | 39, 40, 41, 42 | Pending |
| WEB-06 | S6 | 43, 44, 45, 46, 47 | Pending |

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| screen `login` | estado de carregamento | AC 1 - botão desativado enquanto o pedido corre |
| screen `login` | estado de erro | AC 2, AC 3 |
| screen `login` | estado vazio | n/a - formulário sem lista, não tem estado vazio |
| screen `login` | estado não autorizado | n/a - é o destino de quem não está autorizado |
| screen `login` | densidade e ordenação | n/a - formulário de três campos, sem coleção |
| screen `login` | ação destrutiva confirma | n/a - nenhuma ação do ecrã destrói dados |
| screen `shell` (barra + navegação) | estado não autorizado | AC 8, AC 21 - itens sem permissão não são renderizados |
| screen `shell` | ação destrutiva confirma | AC 11 - Sair pede confirmação no diálogo partilhado |
| screen `shell` | densidade e ordenação | AC 12 - ordem fixa Utilizadores, Roles, Permissões, Tenants, AI |
| screen `users-list` | estado vazio | AC 15 |
| screen `users-list` | estado de carregamento | AC 14 |
| screen `users-list` | estado de erro | AC 16 |
| screen `users-list` | estado não autorizado | AC 10, AC 21 |
| screen `users-list` | densidade e ordenação | AC 13, AC 17 - densidade compacta, `createdAt` desc por omissão |
| screen `users-list` | ação destrutiva confirma | AC 19, AC 20 |
| screen `user-form` | estado de erro | AC 3 - mesma regra de `ValidationProblemDetails` do login |
| screen `user-form` | estado de carregamento | AC 22 - skeleton nos campos até `GET` responder |
| screen `user-detail` | estado não autorizado | AC 10 - `403` em `users/{id}/roles` mostra o bloco vazio, não o ecrã inteiro |
| screen `roles-list` | estado vazio | AC 24 - "Nenhum role" com ação Criar |
| screen `roles-list` | estado de carregamento | AC 14 - mesmo padrão partilhado |
| screen `roles-list` | estado de erro | AC 16 - mesmo padrão partilhado |
| screen `roles-list` | ação destrutiva confirma | AC 30 |
| screen `roles-list` | densidade e ordenação | AC 24 - ordenação por nome asc |
| screen `role-detail` | estado vazio | AC 25 - "Sem permissões atribuídas" |
| screen `role-detail` | ação destrutiva confirma | AC 27 - revogar permissão pede confirmação |
| screen `permissions-list` | estado vazio | AC 32 - "Nenhuma permissão" |
| screen `permissions-list` | ação destrutiva confirma | AC 32 - eliminar permissão pede confirmação |
| screen `permissions-list` | densidade e ordenação | AC 32 - ordenação por `name` asc |
| screen `tenants-list` | estado vazio | AC 33 - "Nenhum tenant" |
| screen `tenants-list` | estado de erro | AC 16 - mesmo padrão partilhado |
| screen `tenants-list` | ação destrutiva confirma | AC 37 - desativar pede confirmação e diz que é reversível por edição |
| screen `tenants-list` | densidade e ordenação | AC 33 - ordenação por `tenantKey` asc |
| screen `tenant-form` | estado de erro | AC 35 |
| screen `ai-chat` | estado vazio | AC 40 - "Faça uma pergunta" antes da primeira mensagem |
| screen `ai-chat` | estado de carregamento | AC 41 |
| screen `ai-chat` | estado de erro | AC 39, AC 42 |
| screen `ai-chat` | ação destrutiva confirma | n/a - limpar histórico local não persiste nada |
| screen `forbidden` | estado não autorizado | AC 10 |
| API consumida `/api/v1/**` | forma do erro | AC 2, AC 3 - ProblemDetails e ValidationProblemDetails, já fixados pela API |
| API consumida `/api/v1/**` | quem pode chamar | AC 4, AC 5, AC 9 - tenant, bearer e permissões |
| API consumida `/api/v1/**` | versionamento | n/a - a API fixa `v1` no caminho e este trabalho não introduz negociação |
| API consumida `/api/v1/identity/login` | comportamento no rate limit | AC 2 - `429` do limiter `auth` cai no mesmo estado de erro do login |
| API exposta pelo front | nenhuma | n/a - o front não expõe API; expõe URLs de aplicação, listadas em `Surface` |
| command `npm run e2e` | formato de saída e códigos de saída | AC 45, AC 46 |
| document `src/web/AGENTS.md` | estrutura e o que o leitor faz a seguir | AC 44 - documenta a regra que o teste de arquitetura impõe |

## Flow

Reutiliza o que a API já decide em vez de duplicar: autorização continua server-side (as policies do `RBAC_MATRIX`), o front só esconde o que o utilizador não pode fazer; a forma do erro é o ProblemDetails que `ExceptionHandlerExtensions` já emite, não um envelope novo; a paginação é a `PaginatedListOutput<T>` existente, não um contrato próprio.

1. browser abre `/login` -> `web/features/identity/login` (new, no door - placement per conventions) - Signal Form com tenant, email, password
2. `web/core/http` (new, door 2) - interceptor acrescenta `X-Tenant` e `Authorization`, e é onde a fila de refresh vive
3. `POST /api/v1/identity/login` -> `Api.Features.Identity` (exists) - devolve `AuthTokenOutput` com `accessToken`, `refreshToken`, `expiresIn`, `user.roles`
4. `web/core/session` (new, door 3) - guarda o refresh em `localStorage`, o access em memória, descodifica o payload para as claims `permission`
5. `web/core/guards` (new, no door - placement per conventions) - `authGuard` e `permissionGuard` leem os signals da sessão antes de cada navegação
6. `web/features/{identity,authorization,tenants,ai}/*` (new, door 1) - um ficheiro por slice: cliente tipado + store de signals + componente
7. out: ecrã renderizado; em `401` o passo 2 chama `POST /api/v1/identity/refresh` (exists) uma vez e repete o pedido original

## Relations

`None - no stored-data shape change` — nenhuma entidade, coluna ou migração da API muda. O único estado persistido é do lado do browser (`localStorage`), e é uma door, não uma entidade: ver door 3 em `Landing`.

## Surface

O front **consome** estas rotas e **não muda** nenhuma delas; a coluna Status é o conjunto que cada ecrã tem de saber renderizar, e é o que vira uma set row por rota no `Coverage` de `checks.md`. `401` e `403` aparecem nas rotas protegidas porque `RequireAuthorization` os produz mesmo sem `ProducesProblem` declarado.

URLs de aplicação expostos pelo front (sem statuses — são rotas do router, não HTTP): `/login`, `/users`, `/users/new`, `/users/:userId`, `/users/:userId/roles`, `/roles`, `/roles/:roleId`, `/permissions`, `/tenants`, `/tenants/:id`, `/ai`, `/forbidden`, `/**` (not-found).

| Route | In | Out | Status |
| --- | --- | --- | --- |
| `POST /api/v1/identity/register` | `email`, `password`, `firstName`, `lastName` | `UserOutput` · ProblemDetails | `201`, `400`, `409` |
| `POST /api/v1/identity/login` | `email`, `password` | `AuthTokenOutput` · ProblemDetails | `200`, `400`, `401`, `409`, `429` |
| `POST /api/v1/identity/refresh` | `refreshToken` | `AuthTokenOutput` · ProblemDetails | `200`, `400`, `401`, `404`, `409`, `429` |
| `GET /api/v1/identity/users` | `pageNumber`, `pageSize`, `searchTerm` | `PaginatedListOutput<UserOutput>` | `200`, `401`, `403` |
| `GET /api/v1/identity/users/{userId}` | `userId` | `UserOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `PUT /api/v1/identity/users/{userId}` | `userId`, `firstName`, `lastName` | `UserOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `DELETE /api/v1/identity/users/{userId}` | `userId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `GET /api/v1/identity/users/{userId}/roles` | `userId` | lista de nomes de role | `200`, `401`, `403` |
| `GET /api/v1/authorization/roles` | `pageNumber`, `pageSize`, `searchTerm` | `PaginatedListOutput<RoleOutput>` | `200`, `401`, `403` |
| `POST /api/v1/authorization/roles` | `name`, `description` | `RoleOutput` · ProblemDetails | `201`, `400`, `401`, `403`, `409` |
| `GET /api/v1/authorization/roles/{roleId}` | `roleId` | `RoleWithPermissionsOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `PUT /api/v1/authorization/roles/{roleId}` | `roleId`, `name`, `description` | `RoleOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `DELETE /api/v1/authorization/roles/{roleId}` | `roleId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `GET /api/v1/authorization/roles/{roleId}/permissions` | `roleId` | `RoleWithPermissionsOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `POST /api/v1/authorization/roles/{roleId}/permissions` | `roleId`, `permissionId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `DELETE /api/v1/authorization/roles/{roleId}/permissions/{permissionId}` | `roleId`, `permissionId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `GET /api/v1/authorization/permissions` | `pageNumber`, `pageSize`, `searchTerm` | `PaginatedListOutput<PermissionOutput>` | `200`, `401`, `403` |
| `POST /api/v1/authorization/permissions` | `name`, `description` | `PermissionOutput` · ProblemDetails | `201`, `400`, `401`, `403`, `409` |
| `PUT /api/v1/authorization/permissions/{permissionId}` | `permissionId`, `name`, `description` | `PermissionOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `DELETE /api/v1/authorization/permissions/{permissionId}` | `permissionId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `GET /api/v1/authorization/users/{userId}/roles` | `userId` | lista de `RoleOutput` | `200`, `401`, `403` |
| `POST /api/v1/authorization/users/{userId}/roles` | `userId`, `roleId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `DELETE /api/v1/authorization/users/{userId}/roles/{roleId}` | `userId`, `roleId` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `GET /api/v1/tenants` | `pageNumber`, `pageSize`, `searchTerm` | `PaginatedListOutput<TenantOutput>` | `200`, `401`, `403` |
| `POST /api/v1/tenants` | `tenantKey`, `displayName`, `contactEmail`, `isolationMode` | `TenantOutput` · ProblemDetails | `201`, `400`, `401`, `403`, `409` |
| `GET /api/v1/tenants/{id}` | `id` | `TenantOutput` · ProblemDetails | `200`, `401`, `403`, `404` |
| `PUT /api/v1/tenants/{id}` | `id`, `displayName`, `contactEmail` | `TenantOutput` · ProblemDetails | `200`, `400`, `401`, `403`, `404` |
| `DELETE /api/v1/tenants/{id}` | `id` | sem corpo · ProblemDetails | `204`, `401`, `403`, `404` |
| `POST /api/v1/ai/chat` | `message`, `history` | `ChatAiResponse` · ProblemDetails | `200`, `401`, `404` |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| Arquitetura VSA espelhada no front | `src/web/src/app/features/{module}/{slice}.ts` — componente, store de signals e cliente HTTP no mesmo ficheiro plano; zero pastas de camada, igual à regra de `src/Api/Features/` | camadas Angular clássicas `core/ shared/ services/ pages/` — contradiz `.cursor/rules/architecture-vsa.mdc` e o teste `Features_ShouldNotContain_LayerFolders`, e o primeiro slice copiado fixa o padrão para todos os seguintes |
| Interceptor único como fronteira HTTP | `src/web/src/app/core/http/api.interceptor.ts` com `X-Tenant` + `Authorization` + fila de refresh partilhada por um único `Subject` | cada slice tratar o seu `401` — sete slices a renovar o token em paralelo invalidam-se entre si, porque `RefreshTokenHandler` roda e revoga o token anterior a cada chamada |
| Persistência da sessão no browser | `localStorage['pt.auth'] = { refreshToken, tenantKey }`; access token só em memória | cookie `httpOnly` — forçado: `LoginHandler` devolve o refresh no corpo e não emite `Set-Cookie`, logo o SPA não tem forma de o mover para um cookie sem mudar a API. `sessionStorage` rejeitado por perder a sessão ao fechar o separador |
| Permissões lidas do payload do JWT | descodificar base64url do segundo segmento do access token e ler as claims `permission` (`AuthorizationClaimTypes.Permission`) | chamar `GET /api/v1/identity/users/{userId}/roles` por navegação — exige a policy `UsersManage`, que um utilizador não-admin não tem, e roles não exprimem policies baseadas em permissão |
| Cliente HTTP tipado escrito à mão | uma função exportada por rota, `Promise`/`resource` tipada com os records da API, no ficheiro do slice | codegen OpenAPI (`ng-openapi-gen`, `@hey-api/openapi-ts`) — `OpenApiHostConfiguration.MapOpenApiHost` só mapeia o documento em Development, logo o CI teria de levantar a API em modo dev para gerar |
| Tenant enviado por chave, não por GUID | header `X-Tenant: <tenantKey>` com o valor de `localStorage['pt.tenant']`, omissão `dev` | `X-Tenant-Id: <guid>` — também suportado pelo `TenantMiddleware`, mas ninguém conhece o GUID do tenant; os tenants semeados são `public` e `dev` |
| `dist/` e `node_modules/` excluídos do template | acrescentar `**/node_modules/**` e `**/dist/**` ao `exclude` de `.template.config/template.json` | deixar como está — `dotnet new install .` passa a empacotar `node_modules`, e isso só se descobre ao gerar o primeiro produto |

- Nada mais nesta mudança é difícil de reverter: escolha de componentes, nomes de ficheiro, número de componentes por ecrã e organização de estilos ficam no diff.

## Impact

| Front | What changes |
| --- | --- |
| domain | novo termo: `tenantKey` passa a ser visível ao utilizador final no ecrã de login — antes era só um detalhe de header lido pelo `SubdomainThenHeaderTenantResolver` |
| domain | novo termo: `sessão` no front — access em memória + refresh em `localStorage`; não existe entidade equivalente server-side, `RefreshToken` continua a ser a autoridade |
| domain | termo existente: `permission` deixa de ser só uma claim consumida pelas policies e passa a decidir o que é renderizado; quem passa a ramificar nele é `permissionGuard` e as diretivas de visibilidade das tabelas — nenhum consumidor server-side muda |
| stored data | nada a migrar do lado do servidor; nenhuma migração EF Core é acrescentada ou alterada |
| stored data | novas chaves no browser: `pt.auth`, `pt.tenant` — sem versão anterior, logo sem backfill; limpar em logout é o AC 11 |
| repositório | `.template.config/template.json` ganha `**/node_modules/**` e `**/dist/**` no `exclude` (door 7); `.gitignore` ganha `node_modules/` e `src/web/dist/` |
| repositório | `AGENTS.md` ganha secção Frontend e a linha de profile `## tlc-spec-lean` / `profile: ui`; novo `src/web/AGENTS.md` como contexto do workspace |
| CI | `.github/workflows/ci.yml` ganha os jobs `web-test` (Node 24, `npm ci`, `npm test`, `npm run build`) e `web-e2e` (`docker compose up -d` + `npm run e2e`, artefacto do relatório) |
| infra | `docker-compose.yml` ganha o serviço `web` (nginx a servir `dist/` e a fazer proxy de `/api` para `api:8080`); novo `src/web/Dockerfile`; `Makefile` ganha `web-verify` |
| API | nenhuma alteração a `src/Api/**` — o dev server usa `proxy.conf.json`, portanto `Cors:AllowedOrigins` fica intacto |
