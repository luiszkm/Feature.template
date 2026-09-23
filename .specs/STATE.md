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
| AD-007 | `GET /api/v1/authorization/roles/{roleId}` (sem `/permissions`) removida do backend e de `features.json`: `RoleWithPermissionsOutput` já é superset de `RoleOutput`, e desde `7c5824f` nada no SPA chamava a rota sem sufixo — ficara sem cliente, mascarada por uma falha na guarda `todas as rotas tem cliente` (comparava só o caminho, não o verbo, então o `PUT`/`DELETE` no mesmo caminho a fazia passar). A guarda agora compara `(método, caminho)` | active |
| AD-008 | `TestServiceFactory.AddCoreServices` (`tests/Api.Tests/Common`) gera um sufixo `Guid.NewGuid()` para o nome da base InMemory, calculado uma vez por `CreateWith*` e não dentro do callback de opções — o EF Core InMemory partilha uma store por *nome*, processo inteiro, e várias classes de teste chamando `nameof(MesmoMétodo)` (nomes repetidos em `Tenants`/`Ai`) corriam em paralelo pela mesma store. Corrige um flake pré-existente (não desta feature) em `UpdateAgentTests`/`GetAgentTests`/`DeactivateAgentTests` e no padrão irmão em `tests/Api.Tests/Tenants/*` | active |
| AD-012 | Um contentor de teste que registe só parte dos módulos e use `AppDbContext` InMemory tem de ter `EnableServiceProviderCaching(false)` (e um `InMemoryDatabaseRoot` próprio se simular reinícios). O EF guarda o modelo por internal service provider, partilhado por todo o InMemory do processo, e o `OnModelCreating` depende dos `ITenantQueryFilterConfigurator` injectados: quem construir o modelo primeiro decide os filtros de tenant de toda a corrida. Era o `DevBootstrapSeederTests` (sem `AddAiModule`) — invisível até a ordem dos testes mudar com `conversas-agente` | active |
| AD-011 | Conversas persistidas (`conversas-agente`, rebase 2026-09-22): transcript em `AiConversations`/`AiConversationItems` na `AppDbContext` partilhada; `POST /ai/chat` recusa `history` com `400` e devolve `conversationId`; posse por linha (`TenantId` + `UserId`) decidida no `IConversationRepository`, `404` para quem não é dono, sem excepção para `Admin`; retenção `Ai:Conversations:RetentionDays` 90 ligada por omissão. Os itens `tool` guardam o que o modelo viu (delimitado) | active |
| AD-010 | Guardrails do agente (`.specs/features/guardrails-agente`, 2026-09-22) entram antes de W2 e absorvem a parte de W7 que os críticos da auditoria Foundry pedem: validador do `history` (só `user`/`assistant` de texto, temporário até W2), erros de tool contidos no loop, spotlighting `<tool_output>` + `IContentGuard`, rate limit `ai` por tenant e quota diária de tokens. `UseRateLimiter` passa a correr depois de `UseAuthorization` | active |
| AD-009 | Comparador de modelos (`.design/comparar-modelos.md`, 2026-09-22) entra antes de W2 (`conversas-agente`). Absorve W1 S1 (ledger `AiUsageEntry` + tokens in/out) e antecipa W5 (modelo por agente). `LlmResponse` ganha `Cost` — OpenRouter devolve `usage.cost`, o que invalida a exclusão "nenhum provider devolve preço" de W1. Comparação só com provider OpenRouter | active |

## Lacunas de implementação

| Estado | Item |
| --- | --- |
| **fechado** | Edição de utilizador era inalcançável: `users/:userId` montava o ecrã de leitura e nenhuma rota montava o `UserForm` com um id. Rota `users/:userId/edit` acrescentada, botões da lista e do detalhe ligados, e um e2e percorre o caminho (C66). O detalhe só mostra Editar a quem tem `identity.user.manage` ou a si próprio, espelhando a policy `UserManageOrSelf` |
| **fechado** | Pesquisa nos quatro ecrãs de lista (C67). Roles, permissões e tenants ganharam a caixa; todos voltam a `pageNumber=1` ao pesquisar |
| **fechado** | Ordenação por cabeçalho (C68), só nos campos que cada `ApplySort` aceita: users `email`/`firstName`/`createdAt`, roles e permissions `name`, tenants `tenantKey`/`displayName`. Sem escolha do utilizador nenhum `sortBy` é enviado e o default da API manda |
| **fechado** | `GET /api/v1/identity/users` com `searchTerm` ou `sortBy=email` derrubava a API com `500` contra Postgres real (`u.Email.Value` não traduz por um `HasConversion` relacional) — invisível nos testes porque `Api.Tests`/`E2ETests` correm em `InMemory`, que aceita a expressão. Corrigido a montante (`fb5deb6`, `0138072`): a comparação usa o `Email` convertido inteiro para igualdade e ordenação; a busca por email só casa endereço completo, não substring — deferido, ver achado 11 |
| **fechado** | C66 (e2e "edita um utilizador") tinha uma condição de corrida: o teste navegava para `/users` logo após o `submit`, sem esperar o botão reativar (`pending()` a `false`), e o `PUT` era cancelado a meio (observado como `499`) — a lista lida antes de o `save` chegar ao servidor. Corrigido esperando `submit` reativar antes de navegar |

## Achados em aberto

Levantados pelos dois Verifiers independentes (ronda 1). Os que tornavam a suite mentirosa foram
corrigidos; estes ficam por decidir e **não** estão provados.

### web-frontend

| # | Achado | Porque ficou |
| --- | --- | --- |
| 1 | 11 elementos que o `Observable` decide não têm check — incluindo duas decisões de arranjo: ordem e membros da navegação (`shell.ts`), e a ação do estado vazio nos quatro ecrãs de lista | precisa de checks novos, não de correção |
| 2 | `POST /api/v1/identity/logout` não tem linha em `Surface` nem em `Coverage` desta feature | está coberto em `auth-cookie-contract` C5, C6, C11 |
| 3 | Seis checks nomeiam dois comportamentos e provam um: C8 (`redirectTo` pós-login — o teste faz a navegação ele próprio), C13 (preservar campos no `429`), C15 (`409` no refresh), C20 (falha ao nível da ligação), C21 (escrita dos query params), C54 (remoção do item de navegação, asserida no signal e não no DOM) | cada um precisa de uma assertion nova |
| 4 | ~~Linha de `Swept` "data lifecycle: C11" diz que `pt.tenant` é removido no logout; o código mantém-no de propósito e `session.store.spec.ts` assere que sobrevive~~ - **fechado nesta ronda**, a linha agora diz o que o código faz | a claim é que está errada, não o código |
| 5 | `Test policy`: 2 das 4 linhas não cumpridas — o ramo `LOCAL_403` do interceptor não tem caso, e os caminhos de erro do ponto de entrada (falha de ligação; query por omissão em `/roles` e `/permissions`) não têm caso | |
| 6 | Drift menor: o plano diz 29 rotas (são 30 antes desta ronda, 28 depois de `AD-007` remover `GetRole`) — fechado o `{id}`→`{tenantId}` e a falta de `sortBy`/`sortDirection` nas quatro rotas de lista; a contagem em si não é perseguida porque `features.json` cresceu com features irmãs (`agentes`, `conversas-agente`) fora do escopo desta | contagem fica aberta de propósito |
| 7 | A busca de utilizadores (C67) só casa um endereço de email completo, não uma substring — `searchTerm=admin` não encontra `admin@producttemplate.com`, só `searchTerm=admin@producttemplate.com` encontra. Nenhum check ou AC fixa "substring" como requisito, e nenhum teste do repositório prova o caso, então nada está a mentir; é uma lacuna de precisão do produto, não da suite | é decisão do produto, não uma correção óbvia |
| 8 | O guard `todas as rotas tem cliente` (`architecture.spec.ts`) comparava só o caminho, não o método — um `PUT`/`DELETE` na mesma URL bastava para "provar" que um `GET` tinha cliente. Corrigido para comparar `(método, caminho)`; foi assim que a regressão do achado 9 escapou | fechado nesta ronda |
| 9 | `role-detail.ts` (`7c5824f`) parou de chamar `GET /roles/{roleId}` sem sufixo, deixando essa rota sem cliente nenhum na SPA — violando a regra do `AGENTS.md`. Resolvido removendo a rota do backend (`AD-007`): `RoleWithPermissionsOutput` já é superset de `RoleOutput` | fechado nesta ronda |

Verificação ronda 3 (`.specs/features/web-frontend/verification.md`): **FAIL** — 61/70 provados na altura do relatório; os quatro achados abertos por esta ronda (10-13) foram fechados logo a seguir, ver notas.

| 10 | ~~C66 (e2e "edita um utilizador") continuava não-determinístico mesmo depois do fix anterior: `toBeEnabled()` sozinho é uma corrida "time-of-check" — `pending()` só fica `true` depois da validação assíncrona do Signal Forms resolver, então o poll do Playwright podia ver o botão no estado original (nunca desativado) e passar de imediato, sem nunca ter esperado o `PUT`. 3 de 5 corridas falharam, 2 com `499` no servidor~~ **fechado**: `page.waitForResponse()` no `PUT` antes de prosseguir — determinístico, independente de quão rápido o round-trip é (contra Postgres local, ~8ms, mais rápido que qualquer intervalo de poll). 10/10 corridas limpas depois | |
| 11 | ~~C67 ("volta a pageNumber=1") tinha mutante sobrevivente: `renderScreen` monta sempre na página 1, então a asserção nunca distinguia "resetou" de "nunca saiu"~~ **fechado**: o teste agora navega para a página 2 (via `changePage`, o mesmo método que o paginador chama) antes de pesquisar. Mutante confirmado morto | |
| 12 | ~~`user-detail.ts`'s `canEdit()` (a própria pessoa vê Editar sem `identity.user.manage`) não tinha teste nenhum, em nenhuma direção~~ **fechado**: C69/C70 acrescentados, dois testes novos, cada mutante (remover o ramo self; forçar sempre `true`) morto isoladamente por um teste diferente | |
| 13 | ~~`plan.md`'s Observable row do `shell` ainda falava em 5 itens fixos; a feature irmã `agentes` acrescentou um 6º ("Agentes") ao mesmo `shell.ts`~~ **fechado**: nota acrescentada; a lacuna de arranjo em si (achado 1) continua aberta, agora contra 6 membros, não 5 | |
| 14 | ~~C68: o terceiro clique no cabeçalho (MatSort `asc`→`desc`→**nenhum**, `disableClear` por omissão) nunca era exercitado - o guard que omite `sortBy`/`sortDirection` nessa reversão podia ser apagado sem nenhum teste acusar~~ **fechado**: terceiro clique acrescentado ao teste existente, mutante confirmado morto (isolado por screen) | achado por uma segunda ronda de fault injection mais precisa, depois do meu primeiro fix do C66/C67 |

### auth-cookie-contract

| # | Achado | Porque ficou |
| --- | --- | --- |
| 7 | As guardas de contrato comparam caminhos e métodos, e **agora também o `429` das rotas com política `auth`** (`EveryRateLimitedRoute_ShouldDeclare_TooManyRequests`). Os restantes statuses continuam sem guarda | fechado só para o caso que falhou; o geral fica |
| 8 | Precision gaps: C1 não assere o valor do `Max-Age`, C10 não assere que o refresh vai sem corpo, C11 não assere a ordem "revogar antes de limpar" | |
| 9 | `docs/security/RBAC_MATRIX.md` é uma linha de `Observable` sem check (o documento está correto; ninguém o verifica) | |

### agentes

Verificação ronda 1 (`.specs/features/agentes/verification.md`): **FAIL**, 31/44 provados.

| # | Achado | Porque ficou |
| --- | --- | --- |
| 10 | ~~Mutante sobrevivente: remover o guard `file.AgentId != agentId` em `ReadAgentFileTool.ExecuteAsync` deixava `ReadAgentFile_ShouldReturnContent_ForSameAgent` verde — nada provava "só desse agente" (AC 39)~~ **fechado**: novo teste `ReadAgentFile_ShouldRejectFile_FromAnotherAgent` (cria dois agentes no mesmo tenant, tenta ler o ficheiro do outro), morto pelo mutante, confirmado | |
| 11 | ~~C20/C21: `--filter "estado de carregamento: agents"` seleciona zero testes e sai `0` — o `it.each($name)` do vitest interpola a string entre aspas simples (`'agents'`), o filtro escrito não~~ **fechado**: proofs corrigidos para citar `'agents'` | |
| 12 | ~~`UpdateAgentTests.Handle_ShouldThrow_WhenAgentDoesNotExist` falhava ~3/5 execuções — três classes de teste com o mesmo nome de método a semear a mesma base InMemory em paralelo~~ **fechado**: `AD-008` | |
| 13 | 9 dos 10 endpoints do módulo só provam no handler, nunca atravessando `Results.Created/Ok/NoContent` e o pipeline HTTP real (C1, C4, C5, C6, C35, C37, C38); `GetAgentFileHandler` (sucesso) não tem prova nenhuma; `DeactivateAgentHandler`'s `409` de último-ativo é só a nível de handler (C11); a cópia do diálogo de confirmar (AC 22) não é asserida em lado nenhum porque `stubConfirm` substitui `ConfirmService.ask` inteiro e descarta os argumentos | precisa de testes novos ao nível HTTP, escopo maior que uma correção pontual |

### Ambas

| # | Achado |
| --- | --- |
| 10 | Passo 5 do `verify.md` (percorrer o fluxo com o utilizador) não correu em nenhuma das features: um sub-agente não tem canal para o utilizador. É a única parte do procedimento que fica por fazer e precisa de uma pessoa. |

### comparar-modelos (achado pela verificação de `observabilidade-agente`, 2026-09-23)

| # | Achado | Porque ficou |
| --- | --- | --- |
| 1 | O S1 do W1 foi absorvido aqui, mas dois comportamentos que o W1 reclamava não têm teste: duas execuções iguais gravam duas linhas (W1 C9) e o rótulo `stub` no gatilho `Development` sem `Ai:Llm:ApiKey` (W1 C5, só o gatilho `Testing` está provado) | não é regressão; precisa de dois testes em `AiUsageTests`, fora do âmbito do W1 |

