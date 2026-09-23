# AgentLoop mantém a pergunta - checks

Profile: ui
Plan: none - change under three files, no one-way door

## Intent

Depois de uma tool call, o `AgentLoop` perde a pergunta do utilizador. A primeira iteração manda
`UserPrompt = mensagem` com o histórico anterior; quando o modelo pede uma tool, o loop acrescenta
ao histórico o turno `assistant` e os `tool`, e põe `userMessage = ""`. A mensagem nunca entrou no
histórico, por isso da segunda iteração em diante o modelo recebe `[...anteriores, assistant(tool_calls), tool]`
sem a pergunta que motivou a tool — responde a algo que já não vê. Afecta chat e comparação, com
qualquer provider (OpenRouter e MAF juntam `UserPrompt` depois do histórico só quando não é vazio).

Quando isto shipped: da segunda chamada em diante, o histórico enviado tem a pergunta como turno
`user` imediatamente antes do `assistant` que pediu a tool. A primeira chamada fica igual. As
`TurnMessages` que o chat persiste continuam sem a pergunta, que o handler grava ele próprio.

4 checks in 1 slice · 0 one-way doors · 0 open

## Checks

### S1 - O modelo vê a pergunta depois de uma tool · LOOP-01 · 2 files · ~6k

**C1** - Com histórico anterior `[user "u0", assistant "a0"]` e mensagem `"q"`, quando a primeira resposta pede uma tool, o segundo pedido ao LLM tem `History` com roles `user, assistant, user, assistant, tool` e conteúdos `u0, a0, q, "", <tool_output>…` por esta ordem, e `UserPrompt` vazio (LOOP-01)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentLoopTurnTests.RunAsync_ShouldKeepUserMessage_BeforeToolCallTurn_OnLaterCalls`

**C2** - O primeiro pedido ao LLM fica como hoje: `UserPrompt` = `"q"` e `History` = só o histórico anterior (2 itens) (LOOP-01)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentLoopTurnTests.RunAsync_ShouldSendMessage_AsUserPrompt_OnFirstCall`

**C3** - Quando o loop esgota `MaxIterations` e pede o resumo, o `History` do pedido de resumo contém exactamente um turno `user` com `"q"` (LOOP-01)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentLoopTurnTests.RunAsync_ShouldKeepUserMessageOnce_InSummaryCall`

**C4** - `AgentResult.TurnMessages` de um turno com uma tool tem roles `assistant, tool` — sem o turno `user` — e o chat persiste `user, assistant, tool, assistant` com um único item `user` (LOOP-01)
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~AgentLoopTurnTests.RunAsync_ShouldExcludeUserMessage_FromTurnMessages`
Proof: `dotnet test tests/Api.Tests --filter FullyQualifiedName~ChatAiHandlerTests.Handle_ShouldPersistToolTurns_InLoopOrder_WithSequenceIncrementingByOne`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| chamadas do loop (3) | primeira C2 · seguintes após tool C1 · resumo pós-`MaxIterations` C3 | - |
| consumidores de `TurnMessages` (1) | `ChatAiHandler` C4 | - |

- `CompareModels` usa o mesmo `RunAsync` sem histórico: coberto pelas mesmas proofs ao nível do loop

## Test policy

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide, sem fronteira (`AgentLoop`) | uma na própria camada | um caso por chamada do loop |

Evidence:

- `AgentLoop.RunAsync`: 3 pontos que montam o pedido (primeira, após tool, resumo) -> decide
- análogo: `AgentLoopGuardrailTests`, mesmo nível

Cost: 4 testes num ficheiro novo.

## Swept

- validation: n/a - nenhum input novo
- failure modes: existing - tool que falha continua a virar erro para o modelo (`AgentLoop.ExecuteToolAsync`, provado em `AgentLoopGuardrailTests`)
- idempotency: n/a - nenhum estado novo
- authorization: n/a - não toca em quem pode chamar
- concurrency: n/a - o loop é por pedido
- data lifecycle: C4 - o que é persistido não muda
- dependency failure: n/a - comportamento do provider inalterado
- state transitions: C1, C3
- observability: n/a - nenhum log novo
