namespace App.Features.Ai;

public sealed class AgentLoop(
    ILlmService llm,
    ToolRegistry toolRegistry,
    ILogger<AgentLoop> logger)
{
    private const int MaxIterations = 5;

    public async Task<AgentResult> RunAsync(
        string userMessage,
        string systemPrompt,
        IReadOnlyList<LlmMessage>? history,
        CancellationToken cancellationToken)
    {
        var conversationHistory = history?.ToList() ?? [];
        var toolDefinitions = toolRegistry.GetDefinitions();
        var iterations = 0;
        var totalTokens = 0;

        while (iterations < MaxIterations)
        {
            iterations++;

            var request = new LlmRequest(
                UserPrompt: userMessage,
                SystemPrompt: systemPrompt,
                History: conversationHistory.Count > 0 ? conversationHistory : null,
                Tools: toolDefinitions.Count > 0 ? toolDefinitions : null);

            var response = await llm.CompleteAsync(request, cancellationToken);
            totalTokens += response.TotalTokens;

            if (response.ToolCalls is not { Count: > 0 })
                return new AgentResult(response.Text, iterations, totalTokens);

            conversationHistory.Add(new LlmMessage("assistant", response.Text));

            foreach (var toolCall in response.ToolCalls)
            {
                logger.LogInformation("Executing tool {ToolName}", toolCall.Name);
                var output = await toolRegistry.ExecuteAsync(toolCall, cancellationToken);
                conversationHistory.Add(new LlmMessage("tool", output, toolCall.Id));
            }

            userMessage = string.Empty;
        }

        logger.LogWarning("AgentLoop reached max iterations ({Max})", MaxIterations);

        var fallback = await llm.CompleteAsync(
            new LlmRequest(
                UserPrompt: "Resuma o que foi encontrado com base nos dados das ferramentas.",
                SystemPrompt: systemPrompt,
                History: conversationHistory),
            cancellationToken);

        totalTokens += fallback.TotalTokens;
        return new AgentResult(fallback.Text, iterations, totalTokens);
    }
}
