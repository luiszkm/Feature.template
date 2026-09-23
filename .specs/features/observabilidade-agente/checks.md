# Observabilidade do loop do agente (W1) - checks

Profile: ui
Plan: `.specs/features/observabilidade-agente/plan.md`

## Intent

26 checks in 2 slices (S1 superseded) · 4 one-way doors · 2 open, of which 0 block (1 blocks go-live)

## Checks

Grouped by the plan's slices; numbering runs across the whole feature. Commands from the repo root.

### S2 - Loop e tools visíveis num trace · OBS-02 · ~9k

**C10** - Com `OpenTelemetry:EnableTraces = true`, um chat regista um span `invoke_agent {nome do agente}` com `gen_ai.operation.name = invoke_agent`, `gen_ai.provider.name` (rótulo em minúsculas), `gen_ai.request.model = Ai:Llm:Model`, `gen_ai.agent.id` e `gen_ai.agent.name` do agente resolvido (OBS-02, AC 10)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.Handle_ShouldStartInvokeAgentSpan_WithGenAiAttributes`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ProviderValue_ShouldMapEveryProviderLabel`

**C11** - Cada chamada a `ILlmService.CompleteAsync` — incluindo a chamada de resumo depois das 5 iterações — regista um span filho `chat {modelo}` com `gen_ai.operation.name = chat`, `gen_ai.request.model`, `gen_ai.usage.input_tokens` e `gen_ai.usage.output_tokens` (OBS-02, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.RunAsync_ShouldStartChatSpan_PerLlmCall_IncludingSummaryFallback`

**C12** - Cada execução de tool pelo `AgentLoop` (que chama o `ToolRegistry`) regista um span filho `execute_tool {nome da tool}` com `gen_ai.operation.name = execute_tool`, `gen_ai.tool.name` e `gen_ai.tool.call.id` (OBS-02, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteAsync_ShouldStartExecuteToolSpan_WithGenAiAttributes`

**C13** - Uma tool fora da allowlist do agente, ou um nome que o `ToolRegistry` não conhece, marca o span `execute_tool` com `error.type = tool_not_found`, mantendo o `{"error":"tool_not_found","tool":"…"}` que o modelo recebe e a continuação do loop (OBS-02, AC 13)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteAsync_ShouldSetToolNotFoundErrorType_WhenToolIsOutsideAllowlist`

**C14** - Uma excepção na chamada ao LLM, ou na execução de uma tool (que o loop converte em `tool_failed`/`permission_denied` para o modelo), marca o span dessa operação com `error.type` igual ao nome do tipo da excepção e estado `Error` (OBS-02, AC 14)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ChatSpan_ShouldSetErrorTypeAndErrorStatus_WhenLlmThrows`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteToolSpan_ShouldSetErrorTypeAndErrorStatus_WhenToolThrows`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteToolSpan_ShouldSetErrorType_WhenToolDeniesPermission`

**C15** - Uma falha do chat por qualquer razão marca o span `invoke_agent` com `error.type` igual ao nome do tipo da excepção que subiu (OBS-02, AC 15)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.InvokeAgentSpan_ShouldSetErrorType_WhenChatFails`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.InvokeAgentSpan_ShouldSetErrorType_ForEveryWayTheChatFails`

**C16** - Nenhum span regista `gen_ai.tool.call.arguments`, `gen_ai.tool.call.result`, `gen_ai.input.messages` ou `gen_ai.output.messages` (OBS-02, AC 16)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.Spans_ShouldNotContain_ContentAttributes`

**C17** - Com `EnableTraces = true`, o span `invoke_agent` é filho do span HTTP do ASP.NET Core, no mesmo `TraceId` (OBS-02, AC 17)
Proof: `dotnet test tests/E2ETests --filter FullyQualifiedName~AiTelemetryE2ETests.InvokeAgentSpan_ShouldShareTraceId_WithAspNetCoreRequestSpan`

**C18** - Com `EnableTraces = false`, não há `TracerProvider` registado no DI e a fonte `Api.Features.Ai` fica sem listeners durante um chat, logo zero spans (OBS-02, AC 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ActivitySource_ShouldHaveNoListeners_WhenTracesDisabled`

**C19** - Com `EnableTraces = false`, `POST /api/v1/ai/chat` continua a responder `200` com `conversationId`, `reply` e `iterationsUsed` (OBS-02, AC 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200WithReplyAndIterationsUsed_WhenTracesDisabled`

**C34** - Um chat que termina com sucesso deixa `gen_ai.conversation.id` no span `invoke_agent` igual ao `conversationId` devolvido na resposta, também quando a conversa foi criada nesse pedido (OBS-02, rebase) *(rebase)*
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.InvokeAgentSpan_ShouldCarryConversationId`

### S3 - Quem paga vê o gasto por agente · OBS-03 · ~11k

Binding screen: `usage` (análogo a `agents-list`: header `h1` `Uso do AI`, `app-list-state`, `mat-table` + `mat-paginator`, sem acção primária, sem pesquisa, sem cabeçalhos ordenáveis).

**C20** - `GET /api/v1/ai/usage` com `ai.agent.read` responde `200` com uma página de linhas agregadas por agente do tenant corrente, cada uma com `agentId`, `agentName`, `calls`, `failures`, `inputTokens`, `outputTokens`, `totalTokens` e `lastUsedAt` (OBS-03, AC 20)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Handle_ShouldReturnAggregatedRows_PerAgent`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReturn200_WithAggregatedRowShape`

**C21** - `GET /api/v1/ai/usage` sem parâmetros ordena por `totalTokens` desc, estável por `agentId`, com `pageNumber = 1` e `pageSize = 20` (OBS-03, AC 21)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Handle_ShouldSortByTotalTokensDesc_StableByAgentId_WhenNoParamsGiven`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldUseDefaultPage_OverHttp`

**C22** - Com `from` e `to`, só contam linhas cujo `createdAt` cai dentro do intervalo, fronteiras incluídas (OBS-03, AC 22)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Handle_ShouldFilterByCreatedAt_WithinInclusiveRange`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReadOffsetRange_AsUtcInstants_OverHttp`

**C23** - `from` posterior a `to` responde `400` com title `Validation failed` (OBS-03, AC 23)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Validator_ShouldFail_WhenFromIsAfterTo`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReturn400_WhenFromIsAfterTo`

**C24** - Um caller autenticado sem `ai.agent.read` recebe `403` em `GET /api/v1/ai/usage` (OBS-03, AC 24)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReturn403_WhenCallerLacksReadPermission`

**C25** - Com `FeatureFlags:EnableAI = false`, `GET /api/v1/ai/usage` responde `404` com title `Feature disabled` (OBS-03, AC 25)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReturn404_WhenEnableAiIsFalse`

**C26** - `/ai/usage` com `ai.agent.read` e `totalCount = 0` mostra o estado vazio do `app-list-state` com a copy exacta `Sem utilização registada` (OBS-03, AC 26)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/usage.spec.ts --filter "estado vazio"`

**C27** - Enquanto a lista de uso está `loading`, mostra `mat-progress-bar` (`data-testid="list-loading"`) e o paginator fica desactivado (OBS-03, AC 27)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de carregamento: 'usage'"`

**C28** - `GET /api/v1/ai/usage` a devolver `500` mostra o `title` do ProblemDetails e o botão `Tentar de novo` do `app-list-state` (OBS-03, AC 28)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de erro repete a query: 'usage'"`

**C29** - Abrir `/ai/usage` sem `ai.agent.read` renderiza o ecrã `forbidden` com a copy `Sem permissão para esta operação` (OBS-03, AC 29)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/usage.spec.ts --filter "sem permissao vai para forbidden"`

**C30** - Com a flag disponível e `ai.agent.read`, a shell mostra o item de navegação `Uso` (`data-testid="nav-ai-usage"`); a `AiAvailability` esconde-o quando a flag responde `404` (OBS-03, AC 30)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "mostra Uso quando ai.agent.read e a flag esta on"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "esconde Uso quando a flag esta off"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "esconde Uso sem ai.agent.read"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "Uso fica depois de Agentes na navegacao"`

**C31** - O cliente do uso vive como ficheiro plano em `src/web/src/app/features/ai/usage.ts`, sem pastas de camada (OBS-03, AC 31)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "todas as rotas de features.json tem cliente"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "sem pastas de camada"`

**C33** - `GET /api/v1/ai/usage` sem autenticação responde `401` (OBS-03, Surface)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReturn401_WhenNotAuthenticated`

**C35** - O ecrã `usage` tem três regiões por esta ordem — `header` só com o `h1` `Uso do AI` (sem acção primária), `app-list-state` com a tabela, `mat-paginator` — sem caixa de pesquisa nem `mat-sort-header`, e colunas `Agente`, `Chamadas`, `Falhas`, `Tokens entrada`, `Tokens saída`, `Total`, `Último uso` por esta ordem (OBS-03, binding S3) *(ronda 1)*
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/usage.spec.ts --filter "arranjo: cabecalho so com titulo, lista e paginador"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/usage.spec.ts --filter "mostra uma linha por agente com os totais"`

**C36** - Cada span `chat` leva `gen_ai.provider.name` (atributo Required nas inference spans da semconv GenAI) com o valor do provider activo — `openrouter`, `microsoft.agent_framework` ou `stub` —, também atravessando o host real (OBS-02, semconv) *(ronda 1)*
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ChatSpan_ShouldCarryProviderName`
Proof: `dotnet test tests/E2ETests --filter FullyQualifiedName~AiTelemetryE2ETests.InvokeAgentSpan_ShouldShareTraceId_WithAspNetCoreRequestSpan`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `GET /api/v1/ai/usage` statuses (5) | 200 C20 · 400 C23 · 401 C33 · 403 C24 · 404 C25 | - |
| GenAI span operation names (3) | `invoke_agent` C10 · `chat` C11 · `execute_tool` C12 | - |
| `chat` span call sites (2) | chamada de iteração C11 · chamada de resumo pós-5ª iteração C11 | - |
| atributos de `invoke_agent` (6) | `gen_ai.operation.name` C10 · `gen_ai.provider.name` C10 · `gen_ai.request.model` C10 · `gen_ai.agent.id` C10 · `gen_ai.agent.name` C10 · `gen_ai.conversation.id` C34 | - |
| span `error.type` outcomes (3) | `tool_not_found` C13 · tipo da excepção no próprio span C14 · tipo da excepção em `invoke_agent` C15 | - |
| `execute_tool` com erro (3) | `tool_not_found` C13 · `UnauthorizedAccessException` (permission_denied) C14 · outra excepção C14 | - |
| causas de falha do `invoke_agent` (5) | excepção do LLM C15 · `NotFoundException` C15 · `BusinessRuleException` C15 · `TooManyRequestsException` C15 · `ContentBlockedException` C15 | - |
| `gen_ai.provider.name` (3 valores) | `openrouter` C10, C36 · `microsoft.agent_framework` C10, C36 · `stub` C10, C36 | - |
| atributos de `chat` (5) | `gen_ai.operation.name` C11 · `gen_ai.request.model` C11 · `gen_ai.usage.input_tokens` C11 · `gen_ai.usage.output_tokens` C11 · `gen_ai.provider.name` C36 | - |
| ecrã `usage` arranjo e copy (4) | regiões e ordem C35 · header só `h1` sem acção C35 · sem pesquisa nem sort C35 · colunas e ordem C35 | - |
| atributos de conteúdo excluídos dos spans (4) | C16, table-driven over all 4 | - |
| one-way doors deste rebase (4) | tokens de entrada/saída no `LlmResponse` C11 · primeira `ActivitySource` do repo C10 · vocabulário GenAI dos spans C10,C11,C12,C34 · contrato de leitura do uso C20 | - |
| Relations entities (1) | `AiUsageEntry` (lido, não mudado) C20 | - |
| screen `usage` states (4) | vazio C26 · loading C27 · erro C28 · forbidden C29 | - |
| shell nav item `Uso` (4) | visível com permissão + flag C30 · escondido quando a flag responde `404` C30 · escondido sem `ai.agent.read` C30 · depois de `Agentes` C30 | - |

- Claims que nomeiam um status code, rota ou forma de resposta: C20, C21, C22, C23, C24, C25, C33 - cada um tem uma prova que atravessa a fronteira HTTP
- Claims que nomeiam um span ou atributo GenAI: C10, C11, C12, C13, C14, C15, C16, C17, C18, C34 - cada um tem uma prova que atravessa a fronteira do `ActivitySource`
- Nenhum outro check reclama mais do que o único caso que a sua prova exercita

## Test policy

O repositório já responde onde vivem os testes de API (`tests/Api.Tests/Ai`) e que contratos HTTP passam por `TestWebApplicationFactory` (ver `agentes/checks.md`). Não responde quantos pontos de decisão da agregação de uso e da instrumentação de spans precisam de prova no próprio nível, porque nenhum dos dois existe ainda no módulo.

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide, atravessado por uma fronteira | uma na fronteira **e** uma no seu próprio nível | o contrato na fronteira; um caso asserido por linha da tabela de decisão no seu próprio nível |
| Decide, não atravessado por uma fronteira | uma no seu próprio nível | um caso asserido por linha da tabela de decisão |
| Ponto de entrada que não decide | uma na fronteira | entrada aceite, cada entrada rejeitada, cada caminho de erro |
| Instrumentação, pass-through | nenhuma própria | coberto pela prova do consumidor |

Evidence:

- `GetAiUsageHandler` (novo): decide sort default, presença/ausência de `from`/`to`, e `from > to` - 3 pontos de ramificação -> decide, atravessado pela fronteira HTTP; análogo mais próximo no repositório é `ListAgentsHandler` (paginação e ordenação default, provado ao nível do handler em `agentes/checks.md` C2) - mesma forma para o default, sem análogo prévio para o intervalo `from`/`to`
- `AgentLoop` com telemetria (os spans de tool vivem onde o loop apanha a excepção desde `guardrails-agente`): mapear tokens e nomes para atributos de span é instrumentação; decidir `error.type` (excepção vs `tool_not_found`) é decisão - 2 pontos de ramificação -> decide, não atravessado por fronteira -> prova no próprio nível (C13, C14, C15)
- `usage.ts` / componente da lista de uso: estado do ecrã (vazio/loading/erro/forbidden) - decide; análogo mais próximo é `agents-list.ts`, já provado a este nível em `agentes/checks.md` (C19-C22, C25)

Cost: provas no próprio nível em 3 ficheiros novos (`GetAiUsageTests`, `AgentTelemetryTests`, `usage.spec.ts`). Sem elas, o intervalo `from > to` e a decisão de `error.type` ficam provados só por um caminho de um teste HTTP que os atravessa.

## Swept

- validation: C23 (intervalo invertido -> `400`), C22 (fronteiras do intervalo incluídas), C21 (defaults de paginação da lista de uso)
- failure modes: C14 (excepção do LLM ou da tool marca o span), C15 (falha do chat marca `invoke_agent`); a gravação da linha de uso é `comparar-modelos`
- idempotency: n/a - nada novo é escrito; o ledger que S3 lê é append-only (`comparar-modelos`)
- authorization: C24, C25 (leitura de uso sob `AiAgentsRead` + flag), C33 (`401` sem autenticação); a leitura do ledger nunca cruza tenant pelo query filter existente (`AiUsageTests.UsageEntries_ShouldBeTenantFiltered`); rate limit n/a - `GET /ai/usage` não gasta LLM e fica fora da policy `ai`
- concurrency: n/a - S3 só lê; a ordem dos spans irmãos dentro de uma iteração não é garantida e nenhum check depende dela
- data lifecycle: n/a - nenhuma linha nova; retenção do ledger continua sem TTL (`comparar-modelos`)
- dependency failure: C14 - a falha do LLM fica no span (`error.type`); sem circuit breaker
- state transitions: n/a - nada muda de estado
- observability: é a feature. C10-C19, C34 (spans, atributos GenAI, parentesco de trace, silêncio quando desligado); exportador fica na pergunta aberta 1

## Handoff

Arithmetic before code, `wc -c` of files each slice touches, divided by four:

- S1 (`AiContracts.cs`, `ChatAi.cs`, `AiModule.cs`, `AiUsageEntry.cs` + repositório + config + migration, `ChatAiUsageTests.cs`, `AiUsageTrackerTests.cs`, `AiUsageEntryShapeTests.cs`, `AiUsageRepositoryTests.cs`, `AiModuleTests.cs`) ~36k chars -> ~9k tokens
- S2 (`ChatAi.cs`, `AgentLoop.cs`, `ToolRegistry.cs`, `AiContracts.cs` (tokens no `LlmResponse`), `ObservabilityConfiguration.cs`, `AiTelemetry.cs` novo, `AgentTelemetryTests.cs`, `AiTelemetryE2ETests.cs`) ~36k chars -> ~9k tokens
- S3 (`GetAiUsage.cs` novo, `AiModule.cs` (rota + policy), `features.json`, `src/Api/openapi.json`, `usage.ts`, `usage.spec.ts`, `shell.ts`, `shell.spec.ts`) ~44k chars -> ~11k tokens
- leitura já em contexto (módulo Ai completo, `agentes/checks.md`, plano) ~35k tokens

Total ~64k, abaixo do orçamento de 150k -> **um único builder**, sem handoff a meio da feature.

Rebase 2026-09-23 sobre `dc21725`: S1 já não é deste builder; S2 abre o span de tool no `AgentLoop` (onde as excepções de tool são apanhadas desde `guardrails-agente`); `invoke_agent` ganha `gen_ai.conversation.id` (C34). Pergunta aberta 1: sem resposta do utilizador — segue o default do plano (spans em processo, sem exportador). Pergunta aberta 2: S3 entra.

## Superseded

S1 (C1–C9) e C32 foram absorvidos por `comparar-modelos` (AD-009) e verificados lá (PASS). Os
nomes de teste originais nunca existiram; o comportamento vive em `tests/Api.Tests/Ai/AiUsageTests.cs`
e em `.specs/features/comparar-modelos/checks.md`. Diferenças face ao texto original: `totalTokens`
deu lugar a `InputTokens`/`OutputTokens` (+ `Cost`), a linha de falha grava `0` tokens em vez de nulo,
e `NoOpAiUsageTracker` desapareceu. Não são verificados nesta feature.

### (superseded) S1 - Custo de cada chat fica registado · OBS-01 · ~9k

**C1** - Um `POST /api/v1/ai/chat` que responde `200` grava exactamente uma linha `AiUsageEntry` no tenant corrente, com `agentId` do agente que o handler resolveu, `provider`/`model` iguais ao par de `LlmServiceResolver.UsageLabels`, `totalTokens` igual a `AgentResult.TotalTokens`, `success = true` e `errorCode = null` (OBS-01, AC 1)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiUsageTests.Handle_ShouldRecordUsageEntry_WhenChatSucceeds`

**C2** - Uma excepção em `AgentLoop.RunAsync` grava uma linha com `success = false`, `errorCode` igual ao nome do tipo da excepção e `totalTokens` nulo (OBS-01, AC 2)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiUsageTests.Handle_ShouldRecordFailedEntryAndRethrow_WhenAgentLoopThrows`

**C3** - A mesma excepção de `AgentLoop.RunAsync` sobe ao caller de `ChatAiHandler.Handle` sem ser substituída por uma falha de gravação (OBS-01, AC 3)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiUsageTests.Handle_ShouldRecordFailedEntryAndRethrow_WhenAgentLoopThrows`

**C4** - Uma `DbUpdateException` ao gravar a linha produz um `LogError` e o `finally` de `ChatAiHandler` não lança, deixando a resposta original do chat seguir (OBS-01, AC 4)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageTrackerTests.TrackAsync_ShouldLogAndSwallow_WhenSaveFails`

**C5** - Com `StubLlmService` resolvido (`Testing`, ou `Development` sem `Ai:Llm:ApiKey`) a linha grava `provider = "stub"` e `model = "stub"`, nos dois gatilhos (OBS-01, AC 5)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiUsageTests.Handle_ShouldRecordStubLabels_WhenEnvironmentIsTesting`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiUsageTests.Handle_ShouldRecordStubLabels_WhenDevelopmentHasNoApiKey`

**C6** - `AiUsageEntry` expõe só os campos de metadados listados (`tenantId`, `agentId`, `provider`, `model`, `module`, `operation`, tokens, latência, `success`, `errorCode`) e nenhum campo para mensagem do utilizador, `history`, argumentos/resultados de tools, ou resposta do modelo (OBS-01, AC 6)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageEntryShapeTests.AiUsageEntry_ShouldExposeOnlyMetadataFields`

**C7** - `IAiUsageRepository` expõe só adicionar e consultar; nenhum método de alterar ou apagar uma linha existente (OBS-01, AC 7)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageRepositoryTests.IAiUsageRepository_ShouldExposeOnly_AddAndQuery`

**C8** - Qualquer leitura de `AiUsageEntry` devolve só linhas do tenant corrente, pelo query filter em `AiTenantQueryFilters` (OBS-01, AC 8)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiUsageRepositoryTests.Query_ShouldReturnOnly_CurrentTenantRows`

**C9** - Duas chamadas de chat do mesmo tenant com a mesma mensagem gravam duas linhas `AiUsageEntry` distintas, sem chave de deduplicação (OBS-01, AC 9)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiUsageTests.Handle_ShouldRecordOneRowPerExecution_WhenCalledTwiceWithSameMessage`

**C32** - `AiModule` regista `IAiUsageTracker` como `AiUsageTracker` (`AddScoped`), não `NoOpAiUsageTracker`, e `NoOpAiUsageTracker` deixa de existir no código (OBS-01, Landing door 6)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AiModuleTests.AddAiModule_ShouldRegisterAiUsageTracker_NotNoOp`
Proof: `dotnet test tests/ArchitectureTests --filter FullyQualifiedName~SliceStructureTests.Ai_ShouldNotContain_NoOpAiUsageTracker`

## Handoff (build 2026-09-23)

- **Boundary:** C10–C31, C33, C34 fechados num só builder sobre `fca5c96`
- **Settled mid-build:** (1) sem resposta à pergunta 1 — spans em processo, sem exportador (documentado em `getting-started.md`). (2) `AddObservability` lê `OpenTelemetry:EnableTraces` no registo de serviços; os settings em memória da `WebApplicationFactory` só existem depois do `Build()`, por isso os testes ligam/desligam o flag com `UseSetting` — sem isso C18/C19 eram verdadeiros por acaso; C18 ganhou um controlo positivo (ligado → `TracerProvider` e listener existem). (3) Testes com `ActivityListener` numa collection xUnit sequencial e filtrados pelo `TraceId` de uma raiz própria. (4) `from`/`to` entram como `DateTimeOffset` e passam a UTC — `DateTime` a partir de `…Z` virava hora local. (5) Proofs C27/C28 citam `'usage'` entre aspas (interpolação do `it.each`). (6) `AgentLoop` recebe `IOptions<LlmOptions>` opcional para o nome do modelo por omissão nos spans
- **Abandoned:** nada

Ronda 1 do Verifier (FAIL): fechado acrescentando proofs e dois checks — nenhuma claim mudou de valor. O span `chat` passou a levar `gen_ai.provider.name` (C36, código novo no `AgentLoop`: recebe `IHostEnvironment` opcional). C17 passou a depender do flag: o listener do teste regista mas não amostra a fonte Ai, e o mesmo teste corre com traces desligados e verifica zero spans Ai. C21/C22 ganharam proof por HTTP (paginação por omissão, `from`/`to` com offset lidos como instantes UTC). C10 distingue modelo de provider com um agente com modelo próprio.

