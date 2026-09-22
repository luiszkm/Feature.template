# Comparar modelos: ecrãs de comparação e histórico

> Build this with **tlc-implement** (`.claude/skills/tlc-implement`).
> Every criterion below becomes a check with a proof, referenced by its number. Nothing under
> `Unresolved` gets settled while building.

## Intent

Depois de `.tasks/comparar-modelos-api.md`, a API já compara modelos e grava o custo, mas só pode ser
usada com `curl`. O admin do tenant, que é quem escolhe o modelo de cada agente, não tem acesso a
ela. Sem ecrã, não há comparação lado a lado nem "aplicar ao agente". Sobra editar o agente às cegas
no `agent-form`.

O que muda: três rotas novas no front, todas em ficheiros planos em `src/web/src/app/features/ai/`.
- `/ai/compare`: escolher agente, prompt, anexos e 2 a 4 modelos, executar, e ver um cartão por modelo com resposta, tokens, custo, latência e estado. Em cada cartão há "Aplicar ao agente".
- `/ai/compare/:comparisonId`: abre uma comparação gravada, só para ler, com "Executar de novo".
- `/ai/comparisons`: histórico paginado.

O layout segue o padrão actual (Angular Material, como `agents-list` e `agent-form`), por decisão do
utilizador.

27 critérios em 4 slices · 0 decisões difíceis de reverter · 2 perguntas em aberto, nenhuma bloqueia

## Criteria

### Histórico `/ai/comparisons`

1. Quando `/ai/comparisons` abre com `ai.agent.read`, então há um header com `h1` `Comparações` e uma `mat-table` com as colunas `Agente`, `Prompt` (`promptPreview`), `Modelos` (ids separados por vírgula), `Custo` e `Data`, na ordem que a API devolve, com `mat-paginator` de `pageSize` 20.
2. Quando `totalCount` é `0`, então o `app-list-state` mostra o estado vazio com a copy `Nenhuma comparação ainda` e, para quem tem `ai.agent.manage`, a acção `Nova comparação` para `/ai/compare`.
3. Enquanto a lista está a carregar, aparece o `mat-progress-bar` com `data-testid="list-loading"`.
4. Se `GET /api/v1/ai/comparisons` falhar com `500`, então aparece o `title` do ProblemDetails e o botão `Tentar de novo`, que repete o pedido.
5. Quando se clica numa linha, a navegação vai para `/ai/compare/{comparisonId}`.
6. A coluna `Custo` mostra `US$` com 4 casas decimais (por exemplo `US$ 0,0012`), ou `—` quando `totalCost` é `null`.
7. O header tem o botão `Nova comparação` só para quem tem `ai.agent.manage`.

### Nova comparação `/ai/compare`

8. Quando `/ai/compare` abre com `ai.agent.manage`, então o formulário tem, por esta ordem:
   - `Agente`: `mat-select` com os agentes activos, e o default seleccionado por omissão.
   - `Prompt`: `textarea`, máximo 4000 caracteres.
   - `Anexos`: `input type=file multiple`, `accept=".txt,.md,.csv,.json"`.
   - `Modelos`: `mat-select multiple` com os ids de `GET /api/v1/ai/models`.
   - o botão `Executar`.
9. Quando o agente seleccionado tem `model` não nulo, então esse modelo já vem seleccionado em `Modelos`.
10. Enquanto houver menos de 2 ou mais de 4 modelos seleccionados, ou o prompt estiver vazio, o botão `Executar` fica desactivado e aparece a dica `Escolha entre 2 e 4 modelos`.
11. Quando se anexa um ficheiro, o conteúdo é lido no browser como texto e aparece como chip com o nome e um botão de remover.
12. Se forem anexados mais de 3 ficheiros, ou se o total passar de 100 000 caracteres, então aparece o erro `Máximo 3 anexos e 100 000 caracteres` e o `Executar` fica desactivado.
13. Enquanto o `POST /api/v1/ai/comparisons` está em curso, o botão mostra `A executar…` desactivado e cada modelo seleccionado tem um cartão em estado de carregamento.
14. Se `GET /api/v1/ai/models` falhar, então o campo `Modelos` é substituído pela mensagem `Catálogo de modelos indisponível` com o botão `Tentar de novo`.
15. Se o `POST` responder `400`, `404` ou `409`, então o `title` e o `detail` do ProblemDetails aparecem acima do botão e os campos mantêm os valores.

### Resultados

16. Quando o `POST` responde `201`, a navegação vai para `/ai/compare/{comparisonId}` e aparece um `mat-card` por resultado, na ordem de `results`. Em ecrãs a partir de 960px os cartões ficam lado a lado; abaixo disso ficam empilhados.
17. Um cartão `Succeeded` mostra o id do modelo como título, a `reply` em texto (com quebras de linha preservadas), e a linha `Entrada {inputTokens} · Saída {outputTokens} tokens · {custo} · {latência} s · {iterationsUsed} iterações`. O custo segue a regra do 6.
18. Um cartão `Failed` mostra `Falhou` e o `errorCode`. Um cartão `TimedOut` mostra `Excedeu o tempo limite`. Nenhum dos dois mostra o botão `Aplicar ao agente`.
19. Acima dos cartões aparece `Custo total: {totalCost}`, seguindo a regra do 6.
20. Quando `/ai/compare/:comparisonId` abre com `ai.agent.read`, carrega `GET /api/v1/ai/comparisons/{id}` e mostra o agente, o prompt, os nomes dos anexos e os cartões dos resultados, sem formulário editável.
21. Se `GET /api/v1/ai/comparisons/{id}` responder `404`, então aparece o `app-not-found` existente (`data-testid="not-found"`) com o título `Comparação não encontrada`. O link `Voltar` desse componente aponta hoje para `/users`, e isso não muda nesta task.
22. Quando alguém com `ai.agent.manage` clica `Executar de novo` numa comparação gravada, abre `/ai/compare` com agente, prompt, modelos e anexos já preenchidos.

### Aplicar ao agente

23. Quando alguém com `ai.agent.manage` clica `Aplicar ao agente` num cartão `Succeeded`, aparece o diálogo `ConfirmService` com o título `Aplicar modelo` e a mensagem `Aplicar {model} ao agente {agentName}?`, com os botões `Aplicar` e `Cancelar`.
24. Quando o diálogo é confirmado, o front faz `GET /api/v1/ai/agents/{agentId}` seguido de `PUT` com `name`, `instructions` e `toolNames` iguais aos lidos e `model` igual ao do cartão. Com sucesso, aparece o snackbar `Modelo aplicado ao agente`.
25. Se o `PUT` falhar, então o snackbar mostra o `title` do ProblemDetails e o agente fica como estava.

### Navegação e acesso

26. Com a flag disponível e `ai.agent.read`, o shell mostra `Comparar` (`data-testid="nav-ai-compare"`) para `/ai/comparisons`, logo a seguir a `Agentes`. Quando `AiAvailability` detecta a flag desligada, o item fica escondido, como `Agentes`.
27. Quem abre `/ai/compare` sem `ai.agent.manage`, ou `/ai/comparisons` ou `/ai/compare/:comparisonId` sem `ai.agent.read`, vê o ecrã `forbidden` com a copy `Sem permissão para esta operação`, pelo `permissionGuard`.

## Out of scope

- Streaming das respostas e cancelar uma comparação a meio.
- Renderizar markdown nas respostas: é texto com as quebras de linha preservadas.
- Filtros ou pesquisa no histórico: a API não os aceita (AD-006).
- Apagar comparações.
- Anexos PDF ou imagem.
- Diff entre respostas.

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| screen `comparisons-list` | empty state | 2 |
| screen `comparisons-list` | loading | 3 |
| screen `comparisons-list` | error | 4 |
| screen `comparisons-list` | unauthorised | 27 |
| screen `comparisons-list` | density and ordering | 1 (ordem da API, `pageSize` 20, sem sort, AD-006) |
| screen `comparisons-list` | destructive action confirms | n/a - só leitura |
| screen `compare` (novo) | empty (primeira vez) | 8, 9 (agente default, modelo do agente pré-seleccionado) |
| screen `compare` (novo) | loading | 13 |
| screen `compare` (novo) | error | 14, 15 |
| screen `compare` (novo) | unauthorised | 27 |
| screen `compare` (novo) | validação | 10, 12 |
| screen `compare/:id` | loading | Unresolved 1 |
| screen `compare/:id` | error / não encontrado | 21 |
| screen `compare/:id` | resultados parciais / todos falham | 17, 18 |
| screen `compare/:id` | destructive action confirms | 23 (aplicar muda o agente) |
| screen `compare/:id` | density and ordering | 16 (ordem de `results`, breakpoint 960px) |
| shell | navegação | 26 |
| document / copy | copy pt-PT | todos os literais acima; revisão em Unresolved 2 |

## Swept

- validation: 10, 12, 15
- failure modes: 14, 15, 18, 25
- idempotency and retry: 13 (botão desactivado durante o pedido impede um segundo `POST` pelo mesmo clique); `Executar de novo` gera uma comparação nova, de propósito (22)
- authorization: 7, 23, 27; existing - `permissionGuard`, `*appHasPermission`
- concurrency and ordering: 24 (GET seguido de PUT: last-write-wins se outro admin editar o agente entre os dois; aceite porque o `UpdateAgent` não tem concorrência optimista hoje), 16 (ordem de `results`)
- data lifecycle: n/a - o front não guarda nada; os anexos vivem só na memória do formulário até ao `POST`
- external-dependency failure: 14 (catálogo), 18 (modelo falhou)
- state transitions: n/a - os estados dos cartões vêm da API e não mudam no front
- observability: n/a - o front não emite telemetria hoje; o uso é gravado na API (task API, 24 e 40)

## Impact

| Front | What changes |
|---|---|
| domain | nenhum termo novo; usa `ComparisonOutput` e `ModelCatalogItem` de `ai.contracts.ts` (task API) |
| front | ficheiros novos `features/ai/compare.ts` (o ecrã junta-se aos clientes já criados pela task API) e `features/ai/comparisons-list.ts`, mais specs `compare.spec.ts` e `comparisons-list.spec.ts` |
| front | `app.routes.ts` ganha 3 rotas; `shell.ts` ganha o item `Comparar`; `shell.spec.ts` passa a contar 7 itens de navegação (STATE achado 1 e 13) |
| stored data | nothing to migrate |
| e2e | `src/web/e2e/`: uma spec nova contra o stub (`stub/model-a`, `stub/model-b`), opcional nesta task |

## Decided

| Decision | Shape | Alternative rejected |
|---|---|---|
| None | Nada nesta task é difícil de reverter: os ecrãs, as rotas do front e a copy mudam num diff. Os contratos são da task API | — |

## Sources

- `.design/comparar-modelos.md`: Journey e B4.
- `.tasks/comparar-modelos-api.md`: contratos `ComparisonOutput`, `GET /api/v1/ai/models` e `PUT` com `model`.
- Layout: `user delegated`, "Padrão atual (Recomendado)", 2026-09-22. O que vincula a interface é `agents-list.ts`, `agent-form.ts`, `shared/list-state.ts` e `shared/screens.ts` (`forbidden`).

## Unresolved

| # | Kind | Question | Until answered |
|---|---|---|---|
| 1 | open | Estado de loading de `/ai/compare/:comparisonId` | `mat-progress-bar` como nas listas; decide-se no diff |
| 2 | open | Copy dos ecrãs (pt-PT, escrita pelo agente com base nos ecrãs existentes) | Os literais acima valem até alguém os rever |
