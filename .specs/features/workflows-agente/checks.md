# Workflows de agentes checks

Profile: ui
Plan: `.specs/features/workflows-agente/plan.md`
Base: `6c038f0` (branch `feat/comparar-modelos`, árvore limpa)

86 checks in 7 slices · 7 one-way doors · 0 open, of which 0 block

Comandos: `dotnet test tests/Api.Tests --filter ...` e `dotnet test tests/ArchitectureTests --filter ...`
(CI, `make verify`); front `cd src/web && npx ng test --no-watch --include <spec> --filter "<nome>"`
(o mesmo runner de `npm test`, `make web-verify`). Sem Playwright nesta feature: o `make web-e2e`
precisa do compose e o fluxo inteiro fica provado por C27 (HTTP + worker real) e pelos specs MSW.

## Checks

### S1 - CRUD de workflows pela API · ~12 files · ~90 KB · ~23k

**C1** - `POST /api/v1/ai/workflows` válido → `201`, `Location: /api/v1/ai/workflows/{workflowId}`, corpo com `workflowId`, `name`, `description`, `isActive=true`, `nodes` (`key`, `agentId`, `instruction`, `x`, `y`), `edges` (`from`, `to`), `createdAt`, `updatedAt` (WF-01, AC 1)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Post_ShouldReturn201_WithLocationAndBody`

**C2** - corpo inválido → `400` com erro na chave indicada, table-driven sobre 12 casos: ciclo A→B→A (`edges`), laço A→A (`edges`), aresta para `key` inexistente (`edges`), `key` repetida (`nodes`), 0 nós (`nodes`), 11 nós (`nodes`), 31 arestas (`edges`), aresta repetida (`edges`), `name` vazio (`name`), `name` com 201 (`name`), `instruction` com 2001 (`nodes[0].instruction`), ciclo de 3 A→B→C→A (`edges`) (WF-01, AC 2, 3, 4, 5)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Post_ShouldReturn400_ForInvalidGraph`

**C3** - um losango A→B, A→C, B→D, C→D não é ciclo → `201` (WF-01, AC 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Post_ShouldAcceptDiamond`

**C4** - detecção de ciclo ao seu próprio nível: `WorkflowGraph.FindCycle` devolve não-nulo para laço, ciclo de 2 e de 3, e nulo para cadeia, losango e nós soltos (WF-01, AC 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Graph_ShouldDetectCycles_OnlyWhenPresent`

**C5** - `agentId` inexistente ou inactivo → `400` na chave `nodes` (2 casos) (WF-01, AC 6)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Post_ShouldReturn400_WhenAgentUnknownOrInactive`

**C6** - `PUT` substitui nome, descrição, nós e arestas por inteiro (nó removido some, aresta nova aparece) → `200` e `updatedAt` > o anterior (WF-01, AC 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Put_ShouldReplaceGraph_AndAdvanceUpdatedAt`

**C7** - `PUT` com ciclo → `400` na chave `edges` e o workflow gravado não muda (WF-01, AC 2, 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Put_ShouldReturn400_AndKeepGraph_WhenCycle`

**C8** - `GET /api/v1/ai/workflows` devolve `workflowId`, `name`, `nodeCount`, `isActive`, `updatedAt`, só activos, por `updatedAt` desc (WF-01, AC 8)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.List_ShouldReturnActiveOnly_NewestUpdatedFirst`

**C9** - `GET /api/v1/ai/workflows/{workflowId}` devolve `x`/`y` fraccionários exactamente como gravados (`12.5`, `-40.25`) (WF-01, AC 9)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Get_ShouldReturnPositionsAsSaved`

**C10** - `DELETE` → `204`, `GET` por id mostra `isActive=false`, e sai da lista (WF-01, AC 10)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Delete_ShouldDeactivate_AndHideFromList`

**C11** - `workflowId` desconhecido → `404` em `GET`, `PUT`, `DELETE`, `POST .../runs` (4 casos) (WF-01, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Routes_ShouldReturn404_ForUnknownWorkflow`

**C12** - workflow de outro tenant → `null` no `IWorkflowRepository` (query filter de tenant) (WF-01, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Repository_ShouldNotFindWorkflow_OfAnotherTenant`

**C13** - utilizador sem `ai.agent.read` nem `ai.agent.manage` → `403` nas 8 rotas (table-driven) (WF-01, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Routes_ShouldReturn403_WithoutPermission`

**C14** - as 8 rotas exigem a policy certa: `AiAgentsManage` nas 4 de escrita e execução, `AiAgentsRead` nas 4 de leitura (lido dos metadados do endpoint) (WF-01, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Routes_ShouldRequireDeclaredPolicy`

**C15** - `FeatureFlags:EnableAI=false` → `404` nas 8 rotas (table-driven) (WF-01, AC 13)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Routes_ShouldReturn404_WhenAiDisabled`

**C16** - modelo EF: índice único `(WorkflowId, Key)` em `AiWorkflowNodes`, único `(WorkflowId, FromKey, ToKey)` em `AiWorkflowEdges`, único `(WorkflowRunId, NodeKey)` em `AiWorkflowRunSteps`, FK `AgentId` com `Restrict`, FK `WorkflowId` do run com `Restrict` (door 1, door 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Model_ShouldDeclareOneWayConstraints`

**C17** - a migration `AddWorkflows` cobre o modelo: nenhuma alteração pendente (door 1, door 2)
Proof: `dotnet ef migrations has-pending-model-changes --project src/Api`

### S2 - Aceitar uma execução · ~6 files · ~60 KB · ~15k

**C18** - `POST .../runs` → `202` com `Location: /api/v1/ai/workflows/{workflowId}/runs/{runId}`, corpo `status=Queued` e um passo `Pending` por nó, enquanto o LLM está bloqueado (nenhuma chamada terminou) (WF-02, AC 14)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldReturn202_BeforeAnyLlmCallCompletes`

**C19** - `input` vazio ou com 4001 → `400` na chave `input` (2 casos) (WF-02, AC 15)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldReturn400_ForInvalidInput`

**C20** - `IContentGuard` bloqueia o `input` → `400` e `GET .../runs` fica vazio (WF-02, AC 16)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldReturn400_AndPersistNothing_WhenGuardBlocks`

**C21** - quota diária esgotada → `429` e `GET .../runs` fica vazio (WF-02, AC 17)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldReturn429_AndPersistNothing_WhenQuotaExhausted`

**C22** - com `Ai:RateLimit:PermitLimit=1`, o segundo `POST .../runs` no mesmo tenant → `429` (WF-02, AC 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldReturn429_WhenAiRateLimitExceeded`

**C23** - a rota `POST .../runs` declara `429` no `openapi.json` (guarda existente sobre toda a rota com limiter) (WF-02, AC 18)
Proof: `dotnet test tests/ArchitectureTests --filter FullyQualifiedName~OpenApiContractTests.EveryRateLimitedRoute_ShouldDeclare_TooManyRequests`

**C24** - o run gravado guarda o principal de quem fez o `POST`: `userId` do admin, `roles` contém `Admin`, `permissions` são as claims `permission` do token (door 3) (WF-02, AC 27)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldSnapshotLauncherPrincipal`

**C25** - o run guarda a cópia do grafo: um `PUT` do workflow depois do `POST .../runs` não muda `nodes`/`edges` do `GET` do run (door 4) (WF-02, AC 31)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldCopyGraph_SoLaterPutDoesNotChangeRun`

**C26** - executar um workflow desactivado → `404` (WF-01, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.PostRun_ShouldReturn404_WhenWorkflowDeactivated`

**C27** - fim-a-fim pelo host: com o worker registado no `Program` (intervalo de 1 s), um run A→B chega a `Succeeded` só por polling do `GET` (WF-02, AC 19, 23; door 5)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.Run_ShouldReachSucceeded_ThroughHostedWorker`

### S3 - Executar no worker · ~6 files · ~70 KB · ~18k

Os testes deste slice chamam `WorkflowRunner.RunOnceAsync` num host com `PollIntervalSeconds=3600`,
para que o laço de fundo nunca corra uma passagem durante o teste.

**C28** - uma passagem reclama o run `Queued`: `status=Running`, `startedAt` preenchido, antes da primeira chamada ao LLM (WF-02, AC 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldClaimRun_BeforeFirstLlmCall`

**C29** - losango A→B, A→C, B→D, C→D: D só arranca depois de B e C terminarem (instantes registados pelo LLM de teste) (WF-02, AC 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldStartNode_OnlyAfterAllPredecessorsSucceeded`

**C30** - quatro raízes com `MaxParallelSteps=3`: o máximo de chamadas simultâneas ao LLM é exactamente 3 (WF-02, AC 20)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldRunReadyNodesConcurrently_UpToMaxParallelSteps`

**C31** - `MaxParallelSteps` omitido lê o default `3` de `appsettings.json` (WF-02, AC 20)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.Options_ShouldDefault_FromAppSettings`

**C32** - mensagem de um nó raiz = `instruction` + `\n\n` + `input`; de um nó com predecessores B e A (A antes de B na ordem dos nós) = `instruction` + `\n\n` + `input` + `\n\n--- a ---\n{saída de A}` + `\n\n--- b ---\n{saída de B}`; sem `instruction`, começa pelo `input` (3 casos, texto exacto) (WF-02, AC 21)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldComposeNodeMessage_InNodeOrder`

**C33** - quando o LLM é chamado para B, o passo A já está gravado (lido noutro scope) com `Succeeded`, `output`, `inputTokens`, `outputTokens`, `cost`, `latencyMs`, `iterationsUsed`, `startedAt`, `finishedAt` (WF-02, AC 22)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldPersistStep_BeforeStartingDependents`

**C34** - todos os passos `Succeeded` → run `Succeeded` com `finishedAt` (WF-02, AC 23)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldMarkRunSucceeded_WhenAllStepsSucceed`

**C35** - A→B e C independente, A lança `HttpRequestException` → A `Failed` com `errorCode=HttpRequestException`, B `Skipped`, C `Succeeded`, run `Failed` (WF-02, AC 24)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldFailStep_SkipDescendants_AndRunIndependentBranch`

**C36** - com `StepTimeoutSeconds=1`, um passo que demora 5 s → `Failed` com `errorCode=Timeout` e o run `Failed` (WF-02, AC 24)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldFailStepWithTimeout_WhenStepExceedsTimeout`

**C37** - agente do nó desactivado depois de gravado → passo `Failed` com `errorCode=AgentUnavailable` e zero chamadas ao LLM para esse nó (WF-02, AC 25)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldFailStepWithAgentUnavailable_WhenAgentInactive`

**C38** - quota esgotada entre o `POST` e o passo → passo `Failed` com `errorCode=QuotaExceeded` e zero chamadas ao LLM (WF-02, AC 26)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldFailStepWithQuotaExceeded_WhenQuotaExhausted`

**C39** - a tool `get_users_summary` corre com o principal do run: principal com role `Admin` → saída sem `permission_denied`; principal só com `ai.agent.manage` → saída com `permission_denied` (2 casos) (WF-02, AC 27; door 3)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldRunToolsWithLauncherPrincipal`

**C40** - o passo corre com o tenant do run: `ITenantContext.TenantId` no scope do passo = `TenantId` do run (WF-02, AC 27)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldRunStepInRunTenant`

**C41** - cada passo terminado grava um `AiUsageEntry` com `operation=workflow`, o `agentId` do nó, modelo, tokens, custo e `success`; um passo falhado grava `success=false` com o `errorCode` (door 7) (WF-02, AC 28)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldTrackUsage_PerStep_WithWorkflowOperation`

**C42** - duas reclamações do mesmo run `Queued` a partir de dois scopes carregados antes de qualquer gravação: só a primeira devolve `true`; a segunda devolve `false` sem excepção (WF-02, AC 29)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.TryClaim_ShouldLetOnlyOneScopeWin`

**C43** - duas passagens `RunOnceAsync` em paralelo sobre um run de 2 nós → o LLM é chamado exactamente 2 vezes (WF-02, AC 29)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_InParallel_ShouldExecuteEachNodeOnce`

**C44** - run `Running` com `startedAt` há 31 min (relógio de teste, `MaxRunMinutes=30`) → `Failed`, `errorCode=Interrupted`, passos `Pending`/`Running` → `Skipped`, passo `Succeeded` intacto; um com 29 min fica `Running` (2 casos) (WF-02, AC 30)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldInterruptRuns_OlderThanMaxRunMinutes`

**C45** - o worker executa o grafo do run: depois de um `PUT` que troca o agente de A, o LLM recebe as instruções do agente antigo (WF-02, AC 31)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldExecuteGraphCopiedToRun`

**C46** - um run com `Graph` ilegível → `LogError` com o `runId` na mensagem, e o run `Queued` seguinte chega a `Succeeded` na mesma passagem (WF-02, AC 32)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.RunOnce_ShouldLogAndContinue_WhenRunProcessingThrows`

**C47** - o laço do `BackgroundService` espera `PollIntervalSeconds` entre passagens e uma passagem que lança não pára o laço (WF-02, AC 32)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~WorkflowRunnerTests.Execute_ShouldKeepPolling_AfterFailedPass`

### S4 - Ler execuções · ~4 files · ~30 KB · ~8k

**C48** - `GET .../runs/{runId}` depois de terminar devolve `runId`, `workflowId`, `status`, `input`, `errorCode`, `createdAt`, `startedAt`, `finishedAt`, `createdByUserId`, `totalCost`, `nodes`, `edges`, e `steps` com `nodeKey`, `agentId`, `status`, `output`, `inputTokens`, `outputTokens`, `cost`, `latencyMs`, `iterationsUsed`, `errorCode`, `startedAt`, `finishedAt` (WF-03, AC 33)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.GetRun_ShouldReturnFullShape`

**C49** - `totalCost` = soma dos `cost` não nulos (`0.001` + `0.002` = `0.003`); `null` quando nenhum passo tem custo (2 casos) (WF-03, AC 34)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.TotalCost_ShouldSumStepCosts_OrBeNull`

**C50** - `GET .../runs` devolve `runId`, `status`, `inputPreview` (200 caracteres de um input de 300), `totalCost`, `createdAt`, `finishedAt`, por `createdAt` desc (WF-03, AC 35)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.ListRuns_ShouldReturnPreviews_NewestFirst`

**C51** - `runId` de outro workflow do mesmo tenant → `404` (WF-03, AC 36)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.GetRun_ShouldReturn404_ForRunOfAnotherWorkflow`

**C52** - run de outro tenant → `null` no `IWorkflowRunRepository` (WF-03, AC 36)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.Repository_ShouldNotFindRun_OfAnotherTenant`

**C53** - os estados expostos são exactamente `Queued`, `Running`, `Succeeded`, `Failed` (run) e `Pending`, `Running`, `Succeeded`, `Failed`, `Skipped` (passo) (WF-03, AC 37)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~RunWorkflowTests.Statuses_ShouldExposeOnlyDeclaredValues`

### S5 - Lista no front · ~5 files · ~40 KB · ~10k

**C54** - `/ai/workflows` mostra colunas `Nome`, `Nós`, `Atualizado`, `Ações` e as linhas na ordem da resposta da API (WF-04, AC 38)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflows-list.spec.ts --filter "mostra colunas e linhas na ordem da API"`

**C55** - enquanto o `GET` não responde, mostra o estado de carregamento partilhado (WF-04, AC 39)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflows-list.spec.ts --filter "mostra o estado de carregamento"`

**C56** - página vazia → `Nenhum workflow ainda` e, com `ai.agent.manage`, o botão `Novo workflow` (WF-04, AC 40)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflows-list.spec.ts --filter "lista vazia mostra mensagem e novo workflow"`

**C57** - `500` na lista → estado de erro com `Tentar de novo`, que repete o pedido (WF-04, AC 41)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflows-list.spec.ts --filter "erro mostra tentar de novo e repete o pedido"`

**C58** - `Desativar` chama o diálogo com a mensagem `Desativar o workflow "Triagem"?` e só envia `DELETE` depois de confirmar; cancelar não envia nada (2 casos) (WF-04, AC 42)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflows-list.spec.ts --filter "desativar confirma com o nome antes do DELETE"`

**C59** - sem `ai.agent.manage`: nem `Novo workflow` nem `Desativar` no DOM (WF-04, AC 43)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflows-list.spec.ts --filter "sem manage esconde novo e desativar"`

**C60** - com IA disponível e `ai.agent.read`, o item `nav-ai-workflows` com texto `Workflows` aparece logo a seguir a `nav-agents`; sem `ai.agent.read` não aparece (2 casos) (WF-04, AC 44)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "mostra Workflows depois de Agentes"`

### S6 - Canvas · ~4 files · ~60 KB · ~15k

**C61** - editor de um workflow existente desenha cada nó em `left: {x}px; top: {y}px` com o nome do agente e a `key`, e uma seta (`data-testid="edge-{from}-{to}"`) por aresta (WF-05, AC 45)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "desenha nos nas posicoes gravadas e setas"`

**C62** - escolher o mesmo agente duas vezes na paleta cria dois nós com `key` distintas derivadas do nome (`triagem`, `triagem-2`) (WF-05, AC 46)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "paleta acrescenta no com key unica"`

**C63** - fim de arrasto para `(200, 150)` move o nó e o `PUT` seguinte envia `x=200`, `y=150` (WF-05, AC 47)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "arrastar move o no e guardar envia a posicao"`

**C64** - clicar na porta de saída de A e depois em B cria a aresta A→B (WF-05, AC 48)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "ligar porta de saida a outro no cria aresta"`

**C65** - com A→B, ligar B→A mostra `Esta ligação criaria um ciclo`; ligar A→B de novo mostra `Ligação já existe`; em ambos o número de arestas não muda (2 casos) (WF-05, AC 49)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "ligacao invalida mostra mensagem e nao e criada"`

**C66** - seleccionar um nó abre o painel com o agente e a `instruction` editável; editar vai no `PUT`; `Remover nó` apaga o nó e as suas arestas (WF-05, AC 50)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "painel do no edita instrucao e remove no com arestas"`

**C67** - seleccionar uma aresta e carregar `Remover ligação` remove-a; a tecla `Delete` com aresta seleccionada também (2 casos) (WF-05, AC 51)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "remover ligacao apaga a aresta"`

**C68** - `Guardar` em `/ai/workflows/new` faz `POST` com nós e arestas e navega para `/ai/workflows/{workflowId}`; num existente faz `PUT` (2 casos) (WF-05, AC 52)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "guardar faz POST e navega ou PUT"`

**C69** - `400` com erro em `edges` mostra essa mensagem e os nós continuam no canvas (WF-05, AC 53)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "400 mostra erro do campo e mantem o canvas"`

**C70** - canvas sem nós mostra `Adicione um agente para começar` e `Guardar` desactivado (WF-05, AC 54)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "sem nos mostra mensagem e desativa guardar"`

**C71** - sem `ai.agent.manage`: sem paleta, sem `Guardar`, sem `Executar`, nós com `cdkDragDisabled`, portas de ligação ausentes (WF-05, AC 55)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "sem manage mostra canvas so de leitura"`

**C72** - sair com alterações por guardar pergunta `Sair sem guardar as alterações?` e só sai se confirmar; sem alterações sai sem perguntar (2 casos) (WF-05, AC 56)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "sair com alteracoes pede confirmacao"`

**C73** - `404` no `GET` do workflow → `Workflow não encontrado` com ligação para `/ai/workflows` (WF-05, AC 57)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "404 mostra workflow nao encontrado"`

### S7 - Executar e acompanhar · ~2 files · ~40 KB · ~10k

**C74** - `Executar` com input envia `POST .../runs` com `{ input }` e mostra a vista do run (`data-testid="run-view"`); com alterações por guardar `Executar` está desactivado com a dica `Guarde antes de executar` (2 casos) (WF-06, AC 58, 59)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "executar envia POST e abre a vista do run"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "alteracoes por guardar desativam executar"`

**C75** - polling: o intervalo por omissão é 2000 ms (`WORKFLOW_RUN_POLL_MS`); com o run `Running` e depois `Succeeded`, pára no terminal (nenhum `GET` depois dele) e pára ao destruir o componente (2 casos) (WF-06, AC 60)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "polling para no estado terminal e ao sair"`

**C76** - cada nó do run mostra o texto do seu estado, table-driven sobre 5: `Pending`→`Pendente`, `Running`→`Em execução` (com `mat-progress-spinner`), `Succeeded`→`Concluído`, `Failed`→`Falhou`, `Skipped`→`Ignorado` (WF-06, AC 61)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "no do run mostra o texto do estado"`

**C77** - seleccionar um nó do run mostra `output`, tokens in/out, custo USD e latência; um passo falhado mostra o `errorCode` (2 casos) (WF-06, AC 62)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "selecionar no do run mostra detalhe do passo"`

**C78** - run terminado mostra o estado final, o custo total (`$0.003`) e a duração; `totalCost` nulo mostra `—` (2 casos) (WF-06, AC 63)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "run terminado mostra estado custo e duracao"`

**C79** - a lista de execuções mostra `Estado`, `Input`, `Custo`, `Início`; vazia mostra `Nenhuma execução ainda`; clicar numa linha abre a vista desse run (3 casos) (WF-06, AC 64)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "lista de execucoes"`

**C80** - `429` no `POST .../runs` mostra o `detail` do `ProblemDetails` e o input continua escrito (WF-06, AC 65)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "429 ao executar mostra mensagem e mantem input"`

**C81** - a vista do run desenha os `nodes`/`edges` do run (2 nós) mesmo quando o workflow actual tem 3 (WF-06, AC 66)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "vista do run usa o grafo do run"`

### Contrato e dependências (transversal)

**C82** - as 8 rotas estão em `features.json` e em `openapi.json`, nos dois sentidos
Proof: `dotnet test tests/ArchitectureTests --filter FullyQualifiedName~OpenApiContractTests`

**C83** - cada rota tem cliente no front e cada caminho chamado existe no `openapi.json`
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts`

**C84** - nenhuma dependência nova no front: `src/web/package.json` igual à base (door 6)
Proof: `git diff --exit-code 6c038f0 -- src/web/package.json`

**C85** - sem token → `401` nas 8 rotas (table-driven) (WF-01, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~CreateWorkflowTests.Routes_ShouldReturn401_WithoutToken`

**C86** - enquanto o `GET` do workflow não responde, o editor mostra o estado de carregamento partilhado e não desenha o canvas (WF-05, AC 45)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/workflow-editor.spec.ts --filter "mostra carregamento antes do canvas"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| validação do grafo (12) | C2, table-driven sobre os 12 casos | - |
| detecção de ciclo (6 formas) | laço C4 · ciclo de 2 C4 · ciclo de 3 C4 · cadeia C4 · losango C3 C4 · nós soltos C4 | - |
| `POST /api/v1/ai/workflows` statuses (5) | 201 C1 · 400 C2 C5 · 401 C85 · 403 C13 · 404 C15 | - |
| `GET /api/v1/ai/workflows` statuses (4) | 200 C8 · 401 C85 · 403 C13 · 404 C15 | - |
| `GET /api/v1/ai/workflows/{workflowId}` statuses (4) | 200 C9 · 401 C85 · 403 C13 · 404 C11 C15 | - |
| `PUT /api/v1/ai/workflows/{workflowId}` statuses (5) | 200 C6 · 400 C7 · 401 C85 · 403 C13 · 404 C11 C15 | - |
| `DELETE /api/v1/ai/workflows/{workflowId}` statuses (4) | 204 C10 · 401 C85 · 403 C13 · 404 C11 C15 | - |
| `POST /api/v1/ai/workflows/{workflowId}/runs` statuses (6) | 202 C18 · 400 C19 C20 · 401 C85 · 403 C13 · 404 C11 C15 C26 · 429 C21 C22 | - |
| `GET /api/v1/ai/workflows/{workflowId}/runs` statuses (4) | 200 C50 · 401 C85 · 403 C13 · 404 C15 | - |
| `GET /api/v1/ai/workflows/{workflowId}/runs/{runId}` statuses (4) | 200 C48 · 401 C85 · 403 C13 · 404 C15 C51 | - |
| policy por rota (8) | C14, table-driven sobre as 8 | - |
| estado do run (4) | `Queued` C18 · `Running` C28 · `Succeeded` C34 · `Failed` C35 C36 C44 | - |
| estado do passo (5) | `Pending` C18 · `Running` C44 · `Succeeded` C33 · `Failed` C35 · `Skipped` C35 C44 | - |
| `errorCode` do passo (4) | nome da excepção C35 · `Timeout` C36 · `AgentUnavailable` C37 · `QuotaExceeded` C38 | - |
| `errorCode` do run (1) | `Interrupted` C44 | - |
| composição da mensagem do nó (3) | raiz com instrução C32 · sem instrução C32 · com 2 predecessores na ordem C32 | - |
| principal no worker (2) | Admin C39 · sem permissão da tool C39 | - |
| `Ai:Workflows` opções (4) | `MaxParallelSteps` C30 C31 · `StepTimeoutSeconds` C36 · `MaxRunMinutes` C44 · `PollIntervalSeconds` C27 C47 | - |
| estados do ecrã `workflows-list` (4) | carregar C55 · vazio C56 · erro C57 · sem permissão C59 | - |
| estados do ecrã `workflow-editor` (4) | carregar C86 · vazio C70 · erro C69 C73 · sem permissão C71 | - |
| estado do passo no canvas (5) | C76, table-driven sobre os 5 | - |
| startup config: `WorkflowRunner` e `Ai:Workflows` (1 assembly partilhada) | `AddAiModule` + `appsettings.json`, usados por `Program` e pelo `TestWebApplicationFactory` - C27 C31 | - |
| one-way doors (7) | 1 C16 C17 · 2 C16 C17 C53 · 3 C24 C39 · 4 C25 C45 · 5 C27 C42 C43 C47 · 6 C84 · 7 C41 | - |

- Claims com status, rota ou shape: C1-C3, C5-C11, C13, C15, C85, C18-C22, C24-C27, C48, C50, C51 - cada um tem proof HTTP via `TestWebApplicationFactory`. C4, C12, C16, C28-C47, C49, C52, C53 provam ao nível do domínio, repositório ou worker, que é onde decidem.

## Test policy

O repositório responde onde os testes vivem e que contratos HTTP passam pelo
`TestWebApplicationFactory`. Não responde a quanto de uma ordenação topológica, de um escalonador
com limite de paralelismo e de uma reclamação concorrente têm de ser provados ao seu próprio
nível - não há DAG nem worker de fila anterior no repositório.

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decides, reached across a boundary | one at the boundary **and** one at its own layer | the contract at the boundary; one asserted case per row of the decision table at its own layer |
| Decides, not reached across a boundary | one at its own layer | one asserted case per row of the decision table |
| Entry point that decides nothing | one at the boundary | accepted input, each rejected input, each error path |
| Instrumentation, pass-throughs | none of its own | covered by its consumer's proof |

Evidence:

- `WorkflowGraph` (validação + ciclo): 7 regras de validação e a detecção de ciclo - decide, atravessado pela fronteira HTTP (`400`) → C2 na fronteira, C4 ao seu nível
- `WorkflowRunner` (escalonador): prontidão por predecessores, limite de paralelismo, propagação de `Skipped`, 4 `errorCode`, estado final, reclamação, varrimento de interrompidos - ~12 pontos de decisão - decide, sem fronteira HTTP (corre num `BackgroundService`) → C28-C47 ao seu nível, e C27 como caminho de fronteira; análogo mais próximo: `ConversationRetentionService`, provado por `RunOnceAsync` em `ConversationRetentionServiceTests`
- `CreateWorkflow`/`UpdateWorkflow`/`RunWorkflow` handlers: validação delegada + guard + quota - entry points → aceites e cada rejeitado em C1-C26
- `workflow-editor.ts`: validação de ligação (ciclo, repetida), `key` única, dirty-state, polling - decide, atravessado pela fronteira HTTP (MSW) → C61-C81; análogo: `agent-form.ts` em `agent-form.spec.ts`

Cost: 20 proofs ao nível do worker e 1 do grafo, além das de fronteira. Sem elas, a ordem, o
paralelismo e a propagação de falha ficariam provados só por C27, um caminho feliz de dois nós que
atravessa uma linha de cada tabela.

## Swept

- validation: C2, C5, C19
- failure modes: C35, C36, C44, C46
- idempotency: n/a - cada `POST .../runs` é uma execução paga nova, como `POST /comparisons`; sem chave de deduplicação (retry do cliente cria um run novo, visível no histórico)
- authorization: C13, C14, C24, C39; rate limit e quota C21, C22, C38
- concurrency: C29, C30, C42, C43
- data lifecycle: C10, C25, C45; retenção de runs fora de âmbito (plano `Out of scope`)
- dependency failure: C35, C36 - falha ou lentidão do LLM fica contida no passo
- state transitions: C18, C28, C34, C35, C44, C53
- observability: C41, C46; spans `chat`/`execute_tool` vêm do `AgentLoop` sem mudança

## Handoff

S1-S7 lêem ~70 KB de `Features/Ai` (slices vizinhos + `AgentLoop` + `CompareModels` + módulo), ~60 KB de
testes Ai (doubles, `CompareModelsTests`, `AiRateLimitTests`, retenção), ~50 KB do front ai
(`agent-form`, `agents-list`, `shell`, contratos) ≈ 45k tokens de leitura; soma dos slices
~99k < 150k → **um batch**, sem handoff. O orquestrador constrói e depois despacha o Verifier sobre
`6c038f0..HEAD`.

- **Ambiente:** o Node local é 24.11.1 e a Angular CLI 22 exige ≥ 24.15; os proofs `npx ng test` falham antes de correr. Correr com `npx -y node@24.15.0 node_modules/@angular/cli/bin/ng.js test --no-watch --include <spec> --filter "<nome>"` (o CI usa `node-version: "24"`, a última 24.x).
- **Settled mid-build:** nenhum esclarecimento do utilizador depois da aprovação.
- **Abandoned:** nada.
- **Nota para o Verifier:** `shell.spec.ts` 'Uso fica depois de Agentes na navegacao' (de `observabilidade-agente`) mudou de `slice(-4)` para `slice(-5)` com `nav-ai-workflows` entre `nav-agents` e `nav-ai-usage` - é a ordem que AC 44 e o `Impact` aprovaram, não um afrouxamento.
