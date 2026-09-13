# STATE

Registo de decisões ao nível do projeto e achados em aberto. Não é um plano; é o que o próximo
agente precisa de saber antes de tocar nestas features.

## Decisions

| ID | Decisão | Estado |
| --- | --- | --- |
| AD-001 | Front-end em `src/web/` com VSA espelhada: slice = ficheiro plano, zero pastas de camada sob `features/` | active |
| AD-002 | Refresh token em cookie `pt_refresh` (`HttpOnly`, `SameSite=Strict`, `Path=/api/v1/identity`), nunca no corpo | active — supersede a door 3 de `web-frontend` |
| AD-003 | `src/Api/openapi.json` versionado é a autoridade entre API, `features.json` e o front; regenerado por `UPDATE_OPENAPI=1 dotnet test tests/E2ETests` | active |
| AD-004 | Clientes HTTP do front escritos à mão, sem codegen; a guarda de contrato cobre o risco | active |
| AD-005 | Projeto renomeado `App` → `Api` (namespaces, assembly, testes, contrato, infra) | active |
| AD-006 | As listas usam o default de ordenação da API (`createdAt` desc, estável por `Id`) até o utilizador escolher. Os cabeçalhos ordenáveis expõem só os campos que cada `ApplySort` aceita — oferecer um campo que o servidor ignora seria mentir na UI | active |

## Lacunas de implementação

| Estado | Item |
| --- | --- |
| **fechado** | Edição de utilizador era inalcançável: `users/:userId` montava o ecrã de leitura e nenhuma rota montava o `UserForm` com um id. Rota `users/:userId/edit` acrescentada, botões da lista e do detalhe ligados, e um e2e percorre o caminho (C66). O detalhe só mostra Editar a quem tem `identity.user.manage` ou a si próprio, espelhando a policy `UserManageOrSelf` |
| **fechado** | Pesquisa nos quatro ecrãs de lista (C67). Roles, permissões e tenants ganharam a caixa; todos voltam a `pageNumber=1` ao pesquisar |
| **fechado** | Ordenação por cabeçalho (C68), só nos campos que cada `ApplySort` aceita: users `email`/`firstName`/`createdAt`, roles e permissions `name`, tenants `tenantKey`/`displayName`. Sem escolha do utilizador nenhum `sortBy` é enviado e o default da API manda |

## Achados em aberto

Levantados pelos dois Verifiers independentes (ronda 1). Os que tornavam a suite mentirosa foram
corrigidos; estes ficam por decidir e **não** estão provados.

### web-frontend

| # | Achado | Porque ficou |
| --- | --- | --- |
| 1 | 11 elementos que o `Observable` decide não têm check — incluindo duas decisões de arranjo: ordem e membros da navegação (`shell.ts`), e a ação do estado vazio nos quatro ecrãs de lista | precisa de checks novos, não de correção |
| 2 | `POST /api/v1/identity/logout` não tem linha em `Surface` nem em `Coverage` desta feature | está coberto em `auth-cookie-contract` C5, C6, C11 |
| 3 | Seis checks nomeiam dois comportamentos e provam um: C8 (`redirectTo` pós-login — o teste faz a navegação ele próprio), C13 (preservar campos no `429`), C15 (`409` no refresh), C20 (falha ao nível da ligação), C21 (escrita dos query params), C54 (remoção do item de navegação, asserida no signal e não no DOM) | cada um precisa de uma assertion nova |
| 4 | Linha de `Swept` "data lifecycle: C11" diz que `pt.tenant` é removido no logout; o código mantém-no de propósito e `session.store.spec.ts` assere que sobrevive | a claim é que está errada, não o código |
| 5 | `Test policy`: 2 das 4 linhas não cumpridas — o ramo `LOCAL_403` do interceptor não tem caso, e os caminhos de erro do ponto de entrada (falha de ligação; query por omissão em `/roles` e `/permissions`) não têm caso | |
| 6 | Drift menor: o plano diz 29 rotas (são 30) e escreve `/api/v1/tenants/{id}` onde o contrato diz `{tenantId}` | |

### auth-cookie-contract

| # | Achado | Porque ficou |
| --- | --- | --- |
| 7 | As guardas de contrato comparam caminhos e métodos, e **agora também o `429` das rotas com política `auth`** (`EveryRateLimitedRoute_ShouldDeclare_TooManyRequests`). Os restantes statuses continuam sem guarda | fechado só para o caso que falhou; o geral fica |
| 8 | Precision gaps: C1 não assere o valor do `Max-Age`, C10 não assere que o refresh vai sem corpo, C11 não assere a ordem "revogar antes de limpar" | |
| 9 | `docs/security/RBAC_MATRIX.md` é uma linha de `Observable` sem check (o documento está correto; ninguém o verifica) | |

### Ambas

| # | Achado |
| --- | --- |
| 10 | Passo 5 do `verify.md` (percorrer o fluxo com o utilizador) não correu em nenhuma das features: um sub-agente não tem canal para o utilizador. É a única parte do procedimento que fica por fazer e precisa de uma pessoa. |
