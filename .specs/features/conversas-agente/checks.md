# Conversas persistidas (threads/runs) - checks

Profile: ui
Plan: `.specs/features/conversas-agente/plan.md`

## Intent

48 checks in 4 slices · 7 one-way doors · 2 open, of which 1 blocks

## Checks

Agrupados pelas slices do plano; a numeração corre ao longo de toda a feature. Todos os comandos
correm a partir da raiz do repositório.

### S1 - O servidor passa a ser dono do histórico · CONV-01 · ~30k

**C1** - `POST /api/v1/ai/chat` sem `conversationId` cria uma `Conversation` do tenant corrente e do `userId` do JWT e responde `200` com `conversationId`, `reply` e `iterationsUsed` (CONV-01, AC 1)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200WithConversationId_WhenConversationIdIsOmitted`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldCreateConversation_OwnedByTenantAndUser_WhenConversationIdIsOmitted`

**C2** - Um turno que termina sem erro deixa a conversa e todos os itens desse turno persistidos numa única chamada a `SaveChangesAsync` (CONV-01, AC 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldCallSaveChangesExactlyOnce_WhenTurnSucceeds`

**C3** - Um `POST /api/v1/ai/chat` com o `conversationId` do próprio caller acrescenta os itens novos a seguir ao maior `sequence` existente e responde `200` com o mesmo `conversationId` (CONV-01, AC 3)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldAppendAfterHighestSequence_WhenConversationIdIsProvided`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200_AndKeepSameConversationId_WhenConversationIdIsProvided`

**C4** - Um turno sobre uma conversa com itens constrói o histórico entregue ao `AgentLoop` a partir dos últimos `Ai:Conversations:HistoryWindow` (default `20`) itens persistidos de role `user`/`assistant`, do mais antigo para o mais recente, e ignora qualquer conteúdo do corpo do pedido (CONV-01, AC 4)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldBuildHistoryFromPersistedItemsOnly_IgnoringRequestBody`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLimitHistoryToLastHistoryWindowUserAndAssistantItems_OldestToNewest`

**C5** - Um `POST /api/v1/ai/chat` cujo corpo traz o campo `history` responde `400` com título `Validation failed` e a chave `history` em `errors`, sem chamar o `ILlmService` e sem persistir nenhum item (CONV-01, AC 5)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Validator_ShouldFail_WhenHistoryIsProvided`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn400_WhenHistoryIsProvided_AndNotCallLlmOrPersist`

**C6** - Um `conversationId` que não existe no tenant corrente, ou que pertence a outro utilizador, responde `404` com título `Not found` e o mesmo corpo nos dois casos (CONV-01, AC 6)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenConversationIdIsUnknown`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenConversationIdBelongsToAnotherUser`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn404_WithSameBody_WhenConversationIsUnknownOrAnotherUsers`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenConversationBelongsToAnotherTenant`

**C7** - Um `agentId` de pedido diferente do agente fixado na conversa responde `409` com título `Business rule violation` e não acrescenta itens (CONV-01, AC 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenAgentIdDiffersFromConversationsFixedAgent`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn409_WhenAgentIdDiffersFromConversationAgent`

**C8** - Um pedido sobre uma conversa cujo agente fixado tem `isActive: false` responde `404` com título `Not found` e não corre o loop (CONV-01, AC 8)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenConversationsAgentIsInactive`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn404_WhenConversationsAgentIsInactive`

**C9** - Quando o `AgentLoop` lança, a contagem de itens da conversa fica igual à de antes do pedido, nenhuma conversa é criada se o `conversationId` vinha omitido, e o handler de exceções responde `500` com título `Unexpected error` (CONV-01, AC 9)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLeaveItemCountUnchanged_WhenAgentLoopThrows`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldNotCreateConversation_WhenAgentLoopThrowsAndConversationIdOmitted`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn500_WhenLlmHttpFails`

**C10** - Os turnos `assistant` com tool calls e os turnos `tool` que lhes respondem são persistidos pela ordem em que o loop os produziu, com `sequence` a crescer de um em um (CONV-01, AC 10)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldPersistToolTurns_InLoopOrder_WithSequenceIncrementingByOne`

**C11** - Cada item acrescentado atualiza `lastActivityAt` da conversa para o instante desse append (CONV-01, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldUpdateLastActivityAt_WhenItemIsAppended`

**C12** - Ao criar a conversa, o `title` deriva-se dos primeiros 80 caracteres da primeira mensagem do utilizador, terminado em `…` quando a mensagem é maior (CONV-01, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldDeriveTitle_FromFirst80CharsOfFirstMessage`

**C13** - Uma conversa que já tem `Ai:Conversations:MaxItems` (default `200`) itens responde `409` com título `Business rule violation`, sem chamar o `ILlmService` (CONV-01, AC 13)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldThrow_WhenConversationReachedMaxItems`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn409_WhenConversationReachedMaxItems`

**C14** - Dois pedidos concorrentes que calculam o mesmo `sequence` na mesma conversa deixam o segundo write falhar com `409` título `Business rule violation` e não sobrescrevem o item já gravado (CONV-01, AC 14)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldFailSecondWriter_WhenConcurrentAppendsCollideOnSequence`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn409_WhenConcurrentAppendsCollide`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.ConversationItem_ShouldHaveUniqueIndex_OnConversationIdAndSequence`

**C15** - Com `FeatureFlags:EnableAI` a `false`, `POST /api/v1/ai/chat` e as três rotas `/api/v1/ai/conversations` respondem `404` com título `Feature disabled` (CONV-01, AC 15)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn404_WhenEnableAiIsFalse`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.List_ShouldReturn404_WhenEnableAiIsFalse`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldReturn404_WhenEnableAiIsFalse`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Delete_ShouldReturn404_WhenEnableAiIsFalse`

**C16** - Quando um pedido de chat termina, com sucesso ou com erro, é escrita uma linha de log que nomeia `tenantId`, `agentId` e `conversationId` (CONV-01, AC 16)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLogTenantAgentAndConversationId_OnCompletion`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLogTenantAgentAndConversationId_WhenAgentLoopThrows`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLogTenantAgentAndConversationId_WhenTurnIsRefusedBeforeTheLoop`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLogTenantAgentAndConversationId_WhenQuotaRefusesTheTurn`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldLogTenantAgentAndConversationId_WhenGuardBlocksOrAgentIsInactive`

**C42** - Com um guard que bloqueia a mensagem, `POST /api/v1/ai/chat` sem `conversationId` responde `400` e nenhuma `Conversation` nem `ConversationItem` existe depois; com a quota esgotada, `429` e o mesmo resultado (CONV-01, AC 42)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldPersistNothing_WhenGuardBlocksMessage`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldPersistNothing_WhenQuotaIsExhausted`

**C43** - O `content` do item `tool` gravado é igual ao `content` da mensagem `tool` entregue ao LLM, e começa por `<tool_output>\n` e termina em `\n</tool_output>` (CONV-01, AC 43)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldPersistToolItem_AsDeliveredToLlm`

**C44** - A janela de histórico entregue ao `AgentLoop` não contém nenhum item `assistant` de `content` vazio, mesmo quando a conversa os tem gravados (CONV-01, AC 44)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldSkipEmptyAssistantItems_InHistoryWindow`

**C45** - Regressão dos `429` do chat depois do rebase: rate limit e quota continuam a responder com os `ProblemDetails` de `guardrails-agente` (CONV-01, Surface)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiRateLimitTests.Chat_ShouldReturn429_WhenTenantExceedsRateLimit`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiRateLimitTests.Chat_ShouldReturn429_WhenDailyTokenQuotaReached`

### S2 - Cada utilizador vê e apaga só as suas conversas · CONV-02 · ~14k

**C17** - `GET /api/v1/ai/conversations` devolve a página (`pageNumber` default `1`, `pageSize` default `20`) só das conversas do tenant corrente cujo dono é o caller, ordenada por `lastActivityAt` desc e `Id` na ausência de `sortBy` (CONV-02, AC 17)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.Handle_ShouldReturnOnlyCallersConversations_OrderedByLastActivityAtDesc`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.List_ShouldReturnOnlyCallersConversations_OverHttp`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.Handle_ShouldFilterBySearchTerm_AndSortByTitleOrLastActivityAt`

**C18** - `GET /api/v1/ai/conversations/{conversationId}` sobre uma conversa do caller devolve os itens de role `user` e `assistant` por `sequence` crescente (CONV-02, AC 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Handle_ShouldReturnUserAndAssistantItems_OrderedBySequence`

**C19** - Com `includeToolItems=true`, a resposta inclui também os itens de role `tool`, com o `content` tal como foi gravado (CONV-02, AC 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Handle_ShouldIncludeToolItems_WhenIncludeToolItemsIsTrue`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldIncludeToolItems_OnlyWhenQueryParameterIsTrue`

**C20** - Uma conversa de outro utilizador do mesmo tenant faz `GET` e `DELETE` responderem `404` com título `Not found`, nunca `403` (CONV-02, AC 20)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Handle_ShouldThrow_WhenConversationBelongsToAnotherUser`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Handle_ShouldThrow_WhenConversationBelongsToAnotherUser`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldReturn404_ForAnotherUsersConversation_EvenForAdmin_OverHttp`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Delete_ShouldReturn404_ForAnotherUsersConversation_EvenForAdmin_OverHttp`

**C21** - `DELETE /api/v1/ai/conversations/{conversationId}` sobre uma conversa do caller apaga a linha e todos os seus itens e responde `204`; um `GET` seguinte do mesmo id responde `404` (CONV-02, AC 21)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Handle_ShouldRemoveConversationAndAllItems_WhenCallerIsOwner`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Get_ShouldReturn404_AfterDelete`

**C22** - A leitura e a escrita de conversas filtram sempre por `TenantId` e por `UserId`, sem exceção para o role `Admin` - um caller `Admin` que não é dono também recebe `404` (CONV-02, AC 22)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Handle_ShouldThrow_WhenCallerIsAdminButNotOwner`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Handle_ShouldThrow_WhenCallerIsAdminButNotOwner`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldReturn404_ForAnotherUsersConversation_EvenForAdmin_OverHttp`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Delete_ShouldReturn404_ForAnotherUsersConversation_EvenForAdmin_OverHttp`

**C23** - `POST /api/v1/ai/chat` e as três rotas de conversas ficam na policy `Authenticated` e respondem sem exigir `ai.agent.read` nem `ai.agent.manage` (CONV-02, AC 23)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200_WithoutAiAgentReadOrManagePermission`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.List_ShouldReturn200_WithoutAiAgentReadOrManagePermission`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldReturn200_WithoutAiAgentReadOrManagePermission`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Delete_ShouldReturn204_WithoutAiAgentReadOrManagePermission`

**C24** - Um caller não autenticado recebe `401` em `POST /api/v1/ai/chat` e nas três rotas `/api/v1/ai/conversations` (CONV-02, AC 24)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn401_WhenNotAuthenticated`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.List_ShouldReturn401_WhenNotAuthenticated`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldReturn401_WhenNotAuthenticated`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Delete_ShouldReturn401_WhenNotAuthenticated`

### S3 - O chat retoma do servidor e existe um ecrã de conversas · CONV-03 · ~16k

Binding (plan `Sources`, "binding for the interface"): `chat.ts`, `agents-list.ts`, `shared/list-state.ts`, `shared/confirm`. `conversations-list` reutiliza o arranjo de `agents-list` - `header` com `h1` + ação primária à direita, `mat-form-field` Pesquisar, `app-list-state`, `mat-table` + `mat-sort` + `mat-paginator`. `chat` mantém o `mat-card` atual; o picker `Agente` e o botão `Nova conversa` passam a partilhar a linha acima do histórico.

**C25** - `/ai` sem `conversationId` na rota mostra a copy `Faça uma pergunta`, o picker `Agente` ativo e o botão `Nova conversa` desativado (CONV-03, AC 25)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "abre sem conversationId mostra pergunta picker ativo e nova conversa desativada"`

**C26** - `/ai/conversations/{conversationId}` carrega os itens por `GET /api/v1/ai/conversations/{conversationId}`, mostra `mat-progress-bar` (`data-testid="chat-loading"`) enquanto o pedido corre, e renderiza só os turnos `user` e `assistant` (CONV-03, AC 26)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "carrega os itens da conversa e mostra so user e assistant"`

**C27** - Um `GET` de conversa que responde `404` mostra a copy `Conversa não encontrada` e continua numa conversa nova, sem `conversationId` (CONV-03, AC 27)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "404 mostra conversa nao encontrada e continua numa conversa nova"`

**C28** - A resposta do primeiro `POST /api/v1/ai/chat` de uma conversa nova guarda o `conversationId` devolvido e navega para `/ai/conversations/{conversationId}` sem recarregar o histórico já em ecrã (CONV-03, AC 28)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "primeira resposta guarda conversationId e navega sem recarregar"`

**C29** - Enquanto a conversa aberta tem pelo menos um item, o picker `Agente` fica desativado; `Nova conversa` leva a `/ai` com o picker outra vez ativo (CONV-03, AC 29)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "picker desativado com itens e nova conversa reativa o picker"`

**C30** - Uma falha de `POST /api/v1/ai/chat` remove da lista o turno otimista do utilizador, mostra `problem.detail` em `data-testid="chat-error"` e repõe a mensagem no campo de entrada (CONV-03, AC 30)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "falha no post remove o turno otimista e repoe o rascunho"`

**C31** - `/ai/conversations` com `totalCount` `0` mostra o estado vazio com a copy `Nenhuma conversa` e a ação `Nova conversa` a apontar para `/ai` (CONV-03, AC 31)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/conversations-list.spec.ts --filter "estado vazio: nenhuma conversa"`

**C32** - Enquanto a lista de conversas está `loading`, o ecrã mostra `mat-progress-bar` (`data-testid="list-loading"`) e o paginador fica desativado (CONV-03, AC 32)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de carregamento: 'conversations'"`

**C33** - Uma lista de conversas que falha com `500` mostra o `title` do ProblemDetails e o botão `Tentar de novo` (CONV-03, AC 33)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de erro repete a query: 'conversations'"`

**C34** - Confirmar o diálogo `Apagar conversa` (mensagem `Apagar {título}? Esta ação não pode ser anulada.`, `confirmLabel` `Apagar`) chama `DELETE /api/v1/ai/conversations/{conversationId}` e remove a linha da tabela; cancelar não emite nenhum pedido HTTP (CONV-03, AC 34)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/conversations-list.spec.ts --filter "apagar com confirm remove a linha"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/conversations-list.spec.ts --filter "apagar cancelado nao emite pedido"`

**C35** - Quando `AiAvailability` aprendeu que a flag está off, os links de navegação `AI`, `Agentes` e `Conversas` ficam ocultos (CONV-03, AC 35)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "esconde AI, Agentes e Conversas quando a flag esta off"`

**C36** - Os clientes ficam em ficheiros planos - `conversations-list.ts` novo e `chat.ts` alterado, sem pastas de camada - e `chat.ts` deixa de enviar `history` e de guardar o histórico só em memória (CONV-03, AC 36)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "todas as rotas de features.json tem cliente"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "sem pastas de camada"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "deixa de enviar history no pedido"`

**C46** - `conversations-list` tem quatro regiões por esta ordem — `header` (`h1` `Conversas` à esquerda, `Nova conversa` → `/ai` à direita), `mat-form-field` `Pesquisar`, `app-list-state` com a tabela dentro, `mat-paginator` —, colunas `Título`, `Atualizada`, acções, com `mat-sort-header` só em `title` e `lastActivityAt`, botão `Apagar` por linha e o título a ligar a `/ai/conversations/{conversationId}` (CONV-03, binding `agents-list`) *(ronda 1)*
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/conversations-list.spec.ts --filter "arranjo e copy: cabecalho, pesquisa, tabela e paginador"`

**C47** - No `chat`, o picker `Agente` (à esquerda) e `Nova conversa` (à direita) partilham uma linha `display: flex; justify-content: space-between` dentro do `mat-card`, acima do histórico (CONV-03, plan S3) *(ronda 1)*
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/chat.spec.ts --filter "arranjo: picker e nova conversa na mesma linha acima do historico"`

**C48** - As rotas de conversas respondem `400`: `pageSize` `0` ou `101` na lista, e `conversationId` `00000000-0000-0000-0000-000000000000` em `GET` e `DELETE` (CONV-02, Surface) *(ronda 1)*
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ListConversationsTests.List_ShouldReturn400_WhenPageSizeIsOutOfRange`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetConversationTests.Get_ShouldReturn400_WhenConversationIdIsEmpty`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~DeleteConversationTests.Delete_ShouldReturn400_WhenConversationIdIsEmpty`

### S4 - Retenção e apagamento configuráveis · CONV-04 · ~8k

**C37** - Com `Ai:Conversations:RetentionDays` maior que `0`, cada passagem do purge apaga as conversas cujo `lastActivityAt` é anterior a `now - RetentionDays`, e com elas todos os seus itens (CONV-04, AC 37)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ConversationRetentionServiceTests.PurgeAsync_ShouldDeleteConversationsOlderThanRetentionDays_WithTheirItems`

**C38** - Com `Ai:Conversations:RetentionDays` igual a `0`, nenhuma conversa é apagada por idade (CONV-04, AC 38)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ConversationRetentionServiceTests.PurgeAsync_ShouldNotDeleteAnyConversation_WhenRetentionDaysIsZero`

**C39** - O purge percorre as conversas de todos os tenants ignorando os query filters e escreve uma linha de log com o número de conversas apagadas (CONV-04, AC 39)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ConversationRetentionServiceTests.PurgeAsync_ShouldIgnoreQueryFiltersAcrossTenants_AndLogDeletedCount`

**C40** - Se uma passagem do purge lançar, o erro é registado e o host continua a arrancar com as passagens seguintes agendadas no intervalo `Ai:Conversations:PurgeIntervalHours` (default `24`) (CONV-04, AC 40)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ConversationRetentionServiceTests.PurgeAsync_ShouldLogAndContinueScheduling_WhenAPurgePassThrows`

**C41** - `appsettings.json` traz o bloco `Ai:Conversations` com `HistoryWindow: 20`, `MaxItems: 200`, `RetentionDays: 90` e `PurgeIntervalHours: 24` (CONV-04, AC 41)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ConversationRetentionServiceTests.Options_ShouldBindDefaults_FromAppsettingsJson`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/v1/ai/chat` statuses (7) | 200 C1,C3 · 400 C5,C42 · 401 C24 · 404 C6,C8,C15 · 409 C7,C13,C14 · 429 C42,C45 · 500 C9 | - |
| recusas antes do turno que não persistem (3) | loop lança C9 · guard bloqueia C42 · quota esgotada C42 | - |
| `GET /api/v1/ai/conversations` statuses (4) | 200 C17 · 400 C48 · 401 C24 · 404 C15 | - |
| `GET /api/v1/ai/conversations/{conversationId}` statuses (4) | 200 C18,C19 · 400 C48 · 401 C24 · 404 C15,C20,C22 | - |
| `DELETE /api/v1/ai/conversations/{conversationId}` statuses (4) | 204 C21 · 400 C48 · 401 C24 · 404 C15,C20,C22 | - |
| one-way doors (7) | 1 transcript persistido C1,C2 · 2 contrato do chat quebra C5 · 3 posse por linha C6,C17,C20,C22 · 4 ordem do transcript C10,C14 · 5 apagar apaga C21 · 6 retenção ligada por omissão C37,C38,C39,C40,C41 · 7 um agente por conversa C7 | - |
| Relations entities (2) | Conversation C1 · ConversationItem C2 | - |
| screens (2) | chat C25,C26,C27,C28,C29,C30,C47 · conversations-list C31,C32,C33,C34,C46 | - |
| lista: pesquisa e ordenação (4) | `searchTerm` C17 · `title` asc C17 · `title` desc C17 · `lastActivityAt` asc C17 (desc por omissão C17) | - |
| conversa inexistente para o caller (3) | id aleatório C6 · outro utilizador C6 · outro tenant C6 | - |
| saídas do chat com linha de log (8) | sucesso C16 · loop lança C16 · conversa desconhecida 404 C16 · agente diferente 409 C16 · `MaxItems` 409 C16 · quota 429 C16 · guard 400 C16 · agente inactivo 404 C16 | - |
| chat copy (2) | `Nova conversa` desativado inicialmente C25 · `Conversa não encontrada` C27 | - |
| conversations-list copy (2) | vazio `Nenhuma conversa` C31 · confirm `Apagar conversa` / `Apagar {título}? Esta ação não pode ser anulada.` C34 | - |
| startup config: bloco `Ai:Conversations` (1 assembly) | `appsettings.json`, partilhado com o `TestWebApplicationFactory` C41 | - |

- Claims que nomeiam status code, rota ou forma de resposta: C1, C3, C5, C6, C7, C8, C9, C13, C14, C15, C17, C20, C21, C23, C24 - cada um tem uma prova que atravessa a fronteira HTTP
- Nenhum outro check reclama mais do que o único caso que a sua prova exercita
- A prova da door 4 (C14) é da tradução do conflito em `409` ao nível do handler, não de um índice único da base de dados: `Api.Tests` corre InMemory (`Database:UseInMemory=true`), que não aplica índices únicos - declarado no plano, não escondido

## Test policy

O repositório já responde onde os testes de API vivem (`tests/Api.Tests/{Módulo}`) e que os
contratos HTTP passam pelo `TestWebApplicationFactory` (ver `.specs/features/agentes/checks.md`).
Não responde a quanto do espaço de decisão da posse por linha, da janela de histórico e da
resolução do conflito de `sequence` este slice tem de assegurar ao seu próprio nível - são três
tabelas de decisão novas, sem módulo anterior no repositório que as tivesse precisado.

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide, reached across a boundary | one at the boundary **and** one at its own level | the contract at the boundary; one asserted case per decision-table row at its own level |
| Decide, not reached across a boundary | one at its own level | one asserted case per decision-table row |
| Entry point that decides nothing | one at the boundary | accepted input, each rejected input, each error path |
| Instrumentation, pass-through | none of its own | covered by the consumer's proof |

Evidence:

- `IConversationRepository`: toda a query decide por `TenantId` **e** por `UserId` (door 3) - decide, atravessado pela fronteira HTTP (`404` na leitura/escrita de outro dono, C6/C20/C22) - análogo mais próximo: `AgentFileRepository`/`CreateAgentFileHandler`, já provado neste nível em `CreateAgentFileTests.Handle_ShouldThrow_WhenAgentIsInAnotherTenant`
- `ChatAiHandler` (janela de histórico, AC 4): decide sobre o role (`user`/`assistant` vs `tool`), o corte nos últimos `HistoryWindow` itens e a ordem cronológica - 3 pontos de decisão - decide, não atravessado por uma fronteira própria (o resultado alimenta o `AgentLoop`, já stubado nos testes existentes)
- Resolução do conflito de `sequence` (door 4, C14): decide, atravessado pela fronteira HTTP (traduz o conflito em `409`) - análogo mais próximo: `CreateAgentHandler`, nome único -> `409`, já provado em `CreateAgentTests`
- `ConversationRetentionService` (door 6, C37-C40): decide sobre idade e sobre ignorar os query filters entre tenants - decide, não atravessado por uma fronteira HTTP (corre num `BackgroundService`) - análogo mais próximo: `LlmStartupGuard`, mesma forma de `IHostedService`
- `conversations-list.ts` / `chat.ts`: estado de ecrã - decide, atravessado pela fronteira HTTP (MSW) - mesma forma já provada em `agents-list.ts` / `chat.ts` (`.specs/features/agentes/checks.md`, C19-C30)

Cost: 3 provas adicionais ao seu próprio nível (janela de histórico, conflito de `sequence`,
purge) além da prova de fronteira que cada uma já tem. Sem estas linhas, a janela de histórico e
a resolução do conflito de `sequence` ficariam provadas apenas por um caminho HTTP feliz que por
acaso as atravessa, e uma segunda ramificação errada passaria verde.

## Swept

- validation and bounds: C5, C12, C13
- failure and partial failure: C2, C9, C40
- idempotency, retry, duplicates: n/a - sem chave de deduplicação nesta ronda (ver `Out of scope` do plano); um `POST` repetido cria um turno novo, como um `Create*` repetido cria uma linha nova; a colisão de `sequence` é concorrência (C14), não retry
- authorization and rate limits: C20, C22, C23, C24; rate limit e quota: C42, C45 (rebase — `guardrails-agente` trouxe-os)
- concurrency and ordering: C10, C14
- data lifecycle: C21, C37, C38, C39, C41
- external-dependency failure: C9 - falha do `ILlmService` responde `500` pelo handler de exceções existente, sem persistir nada
- state transitions: C1, C7, C8, C21
- observability: C16, C39

## Handoff

Aritmética antes de qualquer código, com a estimativa de `wc -c` dos ficheiros que cada slice
escreve ou altera, dividida por quatro:

- S1 (`ChatAi.cs` alterado, `Conversation.cs`, `ConversationItem.cs`, `AiModule.cs` alterado, migration, `ChatAiHandlerTests.cs`/`ChatAiTests.cs` alterados) ~120k chars -> ~30k tokens
- S2 (`ListConversations.cs`, `GetConversation.cs`, `DeleteConversation.cs`, três ficheiros de teste novos) ~56k chars -> ~14k tokens
- S3 (`conversations-list.ts` novo, `chat.ts` alterado, `conversations-list.spec.ts` novo, `chat.spec.ts`/`list-state.spec.ts`/`shell.spec.ts` alterados) ~64k chars -> ~16k tokens
- S4 (`ConversationRetentionService.cs`, `AiModule.cs` (registo), `appsettings.json`, `ConversationRetentionServiceTests.cs`) ~32k chars -> ~8k tokens
- contexto de leitura já fixo (`Ai` module existente, `docs/paridade-foundry.md` §W2, `docs/plano-agentes.md`, `.specs/features/agentes/checks.md` como precedente) ~52k chars -> ~13k tokens

Total ~81k tokens, abaixo do orçamento de 150k -> **um único builder**, sem handoff a meio da
feature.

## Handoff

- **Boundary:** C1–C45 fechados num só builder, sobre `9b0db04` (rebase em `b8010ee`)
- **Settled mid-build:** (1) rebase: a mensagem de um POST falhado sai da lista e volta ao campo; itens `tool` guardam o que o modelo viu (decisões do utilizador). (2) `LastActivityAt` é concurrency token da `Conversation` — é o mecanismo do AC 14; no InMemory o perdedor recebe `409` mas, sem transacções, as suas linhas podem ficar; no Postgres o `SaveChanges` é transaccional e o índice único `(ConversationId, Sequence)` rejeita-as. `Handle_ShouldFailSecondWriter_*` assere o que o InMemory consegue provar. (3) As proofs C32/C33 citam `'conversations'` entre aspas: o `it.each($name)` do vitest interpola assim, e o filtro sem aspas seleccionava zero testes (mesmo achado 11 de `agentes`). (4) `chat.ts` muda o endereço com `Location.replaceState` depois da primeira resposta, para não recriar o componente nem recarregar o histórico (AC 28). (5) Handler tests passam a chamar `TestServiceFactory.SetUser`: o chat exige dono
- **Abandoned:** nada

Ronda 1 do Verifier (FAIL, 36/45): fechado acrescentando proofs — nenhuma claim mudou de valor. C16 era lacuna de código (seis saídas antes do `try` sem log) e foi corrigido no handler. C6, C8, C14, C17, C19, C20 e C22 ganharam proof por HTTP; C14 ganhou também a prova de modelo do índice único e do concurrency token — o rollback do perdedor continua só garantido pelo Postgres (declarado no plano, `Impact` › tests). C33 passa a asserir `Tentar de novo`. C46–C48 cobrem o arranjo dos dois ecrãs e os `400` das rotas de conversas, que o `Surface` não listava.
