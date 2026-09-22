# Observabilidade do loop do agente (W1) - checks

Profile: ui
Plan: `.specs/features/observabilidade-agente/plan.md`

## Intent

33 checks in 3 slices · 7 one-way doors · 2 open, of which 0 block

## Checks

Grouped by the plan's slices; numbering runs across the whole feature. Commands from the repo root.

### S1 - Custo de cada chat fica registado · OBS-01 · ~9k

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

### S2 - Loop e tools visíveis num trace · OBS-02 · ~9k

**C10** - Com `OpenTelemetry:EnableTraces = true`, um chat regista um span `invoke_agent {nome do agente}` com `gen_ai.operation.name = invoke_agent`, `gen_ai.provider.name` (rótulo em minúsculas), `gen_ai.request.model = Ai:Llm:Model`, `gen_ai.agent.id` e `gen_ai.agent.name` do agente resolvido (OBS-02, AC 10)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.Handle_ShouldStartInvokeAgentSpan_WithGenAiAttributes`

**C11** - Cada chamada a `ILlmService.CompleteAsync` — incluindo a chamada de resumo depois das 5 iterações — regista um span filho `chat {modelo}` com `gen_ai.operation.name = chat`, `gen_ai.request.model`, `gen_ai.usage.input_tokens` e `gen_ai.usage.output_tokens` (OBS-02, AC 11)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.RunAsync_ShouldStartChatSpan_PerLlmCall_IncludingSummaryFallback`

**C12** - Cada execução de tool pelo `ToolRegistry` regista um span filho `execute_tool {nome da tool}` com `gen_ai.operation.name = execute_tool`, `gen_ai.tool.name` e `gen_ai.tool.call.id` (OBS-02, AC 12)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteAsync_ShouldStartExecuteToolSpan_WithGenAiAttributes`

**C13** - Uma tool fora da allowlist do agente, ou um nome que o `ToolRegistry` não conhece, marca o span `execute_tool` com `error.type = tool_not_found`, mantendo a resposta `{"error":"..."}` e a continuação do loop (OBS-02, AC 13)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteAsync_ShouldSetToolNotFoundErrorType_WhenToolIsOutsideAllowlist`

**C14** - Uma excepção na chamada ao LLM, ou na execução de uma tool, marca o span dessa operação com `error.type` igual ao nome do tipo da excepção e estado `Error` (OBS-02, AC 14)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ChatSpan_ShouldSetErrorTypeAndErrorStatus_WhenLlmThrows`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ExecuteToolSpan_ShouldSetErrorTypeAndErrorStatus_WhenToolThrows`

**C15** - Uma falha do chat por qualquer razão marca o span `invoke_agent` com `error.type` igual ao nome do tipo da excepção que subiu (OBS-02, AC 15)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.InvokeAgentSpan_ShouldSetErrorType_WhenChatFails`

**C16** - Nenhum span regista `gen_ai.tool.call.arguments`, `gen_ai.tool.call.result`, `gen_ai.input.messages` ou `gen_ai.output.messages` (OBS-02, AC 16)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.Spans_ShouldNotContain_ContentAttributes`

**C17** - Com `EnableTraces = true`, o span `invoke_agent` é filho do span HTTP do ASP.NET Core, no mesmo `TraceId` (OBS-02, AC 17)
Proof: `dotnet test tests/E2ETests --filter FullyQualifiedName~AiTelemetryE2ETests.InvokeAgentSpan_ShouldShareTraceId_WithAspNetCoreRequestSpan`

**C18** - Com `EnableTraces = false`, não há `TracerProvider` registado no DI e a fonte `Api.Features.Ai` fica sem listeners durante um chat, logo zero spans (OBS-02, AC 18)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentTelemetryTests.ActivitySource_ShouldHaveNoListeners_WhenTracesDisabled`

**C19** - Com `EnableTraces = false`, `POST /api/v1/ai/chat` continua a responder `200` com `reply` e `iterationsUsed` inalterados (OBS-02, AC 19)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiTests.ChatAi_ShouldReturn200WithReplyAndIterationsUsed_WhenTracesDisabled`

### S3 - Quem paga vê o gasto por agente · OBS-03 · ~11k

Binding screen: `usage` (análogo a `agents-list`: header `h1` `Uso do AI`, `app-list-state`, `mat-table` + `mat-paginator`, sem acção primária, sem pesquisa, sem cabeçalhos ordenáveis).

**C20** - `GET /api/v1/ai/usage` com `ai.agent.read` responde `200` com uma página de linhas agregadas por agente do tenant corrente, cada uma com `agentId`, `agentName`, `calls`, `failures`, `inputTokens`, `outputTokens`, `totalTokens` e `lastUsedAt` (OBS-03, AC 20)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Handle_ShouldReturnAggregatedRows_PerAgent`

**C21** - `GET /api/v1/ai/usage` sem parâmetros ordena por `totalTokens` desc, estável por `agentId`, com `pageNumber = 1` e `pageSize = 20` (OBS-03, AC 21)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Handle_ShouldSortByTotalTokensDesc_StableByAgentId_WhenNoParamsGiven`

**C22** - Com `from` e `to`, só contam linhas cujo `createdAt` cai dentro do intervalo, fronteiras incluídas (OBS-03, AC 22)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Handle_ShouldFilterByCreatedAt_WithinInclusiveRange`

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
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de carregamento: usage"`

**C28** - `GET /api/v1/ai/usage` a devolver `500` mostra o `title` do ProblemDetails e o botão `Tentar de novo` do `app-list-state` (OBS-03, AC 28)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shared/list-state.spec.ts --filter "estado de erro repete a query: usage"`

**C29** - Abrir `/ai/usage` sem `ai.agent.read` renderiza o ecrã `forbidden` com a copy `Sem permissão para esta operação` (OBS-03, AC 29)
Proof: `cd src/web && npx ng test --no-watch --include src/app/features/ai/usage.spec.ts --filter "sem permissao vai para forbidden"`

**C30** - Com a flag disponível e `ai.agent.read`, a shell mostra o item de navegação `Uso` (`data-testid="nav-ai-usage"`); a `AiAvailability` esconde-o quando a flag responde `404` (OBS-03, AC 30)
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "mostra Uso quando ai.agent.read e a flag esta on"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/shell/shell.spec.ts --filter "esconde Uso quando a flag esta off"`

**C31** - O cliente do uso vive como ficheiro plano em `src/web/src/app/features/ai/usage.ts`, sem pastas de camada (OBS-03, AC 31)
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "todas as rotas de features.json tem cliente"`
Proof: `cd src/web && npx ng test --no-watch --include src/app/architecture.spec.ts --filter "sem pastas de camada"`

**C33** - `GET /api/v1/ai/usage` sem autenticação responde `401` (OBS-03, Surface)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GetAiUsageTests.Get_ShouldReturn401_WhenNotAuthenticated`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `GET /api/v1/ai/usage` statuses (5) | 200 C20 · 400 C23 · 401 C33 · 403 C24 · 404 C25 | - |
| GenAI span operation names (3) | `invoke_agent` C10 · `chat` C11 · `execute_tool` C12 | - |
| `chat` span call sites (2) | chamada de iteração C11 · chamada de resumo pós-5ª iteração C11 | - |
| span `error.type` outcomes (3) | `tool_not_found` C13 · tipo da excepção no próprio span C14 · tipo da excepção em `invoke_agent` C15 | - |
| `AiUsageEntry` campos de conteúdo excluídos (4) | C6, table-driven over all 4 | - |
| atributos de conteúdo excluídos dos spans (4) | C16, table-driven over all 4 | - |
| gatilho do rótulo `stub` (2) | ambiente `Testing` C5 · `Development` sem `ApiKey` C5 | - |
| one-way doors (7) | ledger persistido C1 · `AgentId` no registo C1 · tokens de entrada/saída no `LlmResponse` C11 · primeira `ActivitySource` do repo C10 · vocabulário GenAI dos spans C10,C11,C12 · usage deixa de ser opcional C32 · contrato de leitura do uso C20 | - |
| Relations entities (1) | `AiUsageEntry` C1 | - |
| screen `usage` states (4) | vazio C26 · loading C27 · erro C28 · forbidden C29 | - |
| shell nav item `Uso` (2) | visível com permissão + flag C30 · escondido quando a flag responde `404` C30 | - |

- Claims que nomeiam um status code, rota ou forma de resposta: C1, C20, C21, C22, C23, C24, C25, C33 - cada um tem uma prova que atravessa a fronteira HTTP
- Claims que nomeiam um span ou atributo GenAI: C10, C11, C12, C13, C14, C15, C16, C17, C18 - cada um tem uma prova que atravessa a fronteira do `ActivitySource`
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

- `ChatAiHandler`'s `finally` (existente, ganha `AgentId` e passa a gravar de facto): decide entre sucesso/excepção, stub/provider real e save-ok/save-falha - 3 pontos de ramificação -> decide, já atravessado pela fronteira HTTP em `ChatAiTests`; esta feature acrescenta a prova no próprio nível (C1-C5, C9)
- `AiUsageTracker.TrackAsync` (novo): decide entre gravar e engolir a falha com `LogError` - 1 ponto de ramificação -> decide, não atravessado por fronteira -> prova no próprio nível (C4)
- `GetAiUsageHandler` (novo): decide sort default, presença/ausência de `from`/`to`, e `from > to` - 3 pontos de ramificação -> decide, atravessado pela fronteira HTTP; análogo mais próximo no repositório é `ListAgentsHandler` (paginação e ordenação default, provado ao nível do handler em `agentes/checks.md` C2) - mesma forma para o default, sem análogo prévio para o intervalo `from`/`to`
- `AgentLoop`/`ToolRegistry` com telemetria: mapear tokens e nomes para atributos de span é instrumentação; decidir `error.type` (excepção vs `tool_not_found`) é decisão - 2 pontos de ramificação -> decide, não atravessado por fronteira -> prova no próprio nível (C13, C14, C15)
- `usage.ts` / componente da lista de uso: estado do ecrã (vazio/loading/erro/forbidden) - decide; análogo mais próximo é `agents-list.ts`, já provado a este nível em `agentes/checks.md` (C19-C22, C25)

Cost: 4 provas no próprio nível em 4 ficheiros novos (`AiUsageTrackerTests`, `GetAiUsageTests`, `AgentTelemetryTests`, `usage.spec.ts`). Sem elas, o intervalo `from > to` e a decisão de `error.type` ficam provados só por um caminho de um teste HTTP que os atravessa.

## Swept

- validation: C23 (intervalo invertido -> `400`), C22 (fronteiras do intervalo incluídas), C21 (defaults de paginação da lista de uso); limites de coluna de `provider`/`model`/`errorCode` decidem-se no diff
- failure modes: C2, C3 (excepção do `AgentLoop` grava e sobe inalterada), C4 (falha ao gravar não derruba o `finally`)
- idempotency: C9 - sem chave de deduplicação; um pedido repetido grava uma segunda linha, de propósito
- authorization: C24, C25 (leitura de uso sob `AiAgentsRead` + flag), C33 (`401` sem autenticação), C8 (a leitura da ledger nunca cruza tenant); rate limit n/a - nenhuma rota do módulo Ai tem limiter hoje e o limiter é W7
- concurrency: C9 - append-only, sem índice único e sem contenção; nenhuma linha é actualizada, logo não há last-write-wins a decidir. A ordem dos spans irmãos dentro de uma iteração não é garantida e nenhum check depende dela
- data lifecycle: C7 (append-only), C6 (nenhuma linha guarda conteúdo do utilizador, logo sem obrigação extra de retenção); sem TTL e sem purge nesta ronda
- dependency failure: C2, C14 - a falha do LLM fica registada na linha (`success = false`) e no span (`error.type`); sem circuit breaker e sem fallback silencioso
- state transitions: n/a - uma linha de `AiUsageEntry` é escrita uma vez e nunca muda (C7); o agregado `Agent` não ganha nem perde transições nesta feature
- observability: é a feature. C1-C9 (uma linha por execução), C10-C19 (spans, atributos GenAI, parentesco de trace, silêncio quando desligado), C4 (`LogError` quando a própria gravação falha). Sem métricas OTel e sem percentis - ambos em Out of scope do plano

## Handoff

Arithmetic before code, `wc -c` of files each slice touches, divided by four:

- S1 (`AiContracts.cs`, `ChatAi.cs`, `AiModule.cs`, `AiUsageEntry.cs` + repositório + config + migration, `ChatAiUsageTests.cs`, `AiUsageTrackerTests.cs`, `AiUsageEntryShapeTests.cs`, `AiUsageRepositoryTests.cs`, `AiModuleTests.cs`) ~36k chars -> ~9k tokens
- S2 (`ChatAi.cs`, `AgentLoop.cs`, `ToolRegistry.cs`, `AiContracts.cs` (tokens no `LlmResponse`), `ObservabilityConfiguration.cs`, `AiTelemetry.cs` novo, `AgentTelemetryTests.cs`, `AiTelemetryE2ETests.cs`) ~36k chars -> ~9k tokens
- S3 (`GetAiUsage.cs` novo, `AiModule.cs` (rota + policy), `features.json`, `src/Api/openapi.json`, `usage.ts`, `usage.spec.ts`, `shell.ts`, `shell.spec.ts`) ~44k chars -> ~11k tokens
- leitura já em contexto (módulo Ai completo, `agentes/checks.md`, plano) ~35k tokens

Total ~64k, abaixo do orçamento de 150k -> **um único builder**, sem handoff a meio da feature.
