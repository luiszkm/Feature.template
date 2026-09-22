namespace Api.Features.Ai;

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
        IReadOnlyList<string>? allowedToolNames = null,
        CancellationToken cancellationToken = default,
        string? model = null)
    {
        var conversationHistory = history?.ToList() ?? [];
        var toolDefinitions = toolRegistry.GetDefinitions(allowedToolNames);
        var iterations = 0;
        var usage = new UsageTotals();

        while (iterations < MaxIterations)
        {
            iterations++;

            var request = new LlmRequest(
                UserPrompt: userMessage,
                SystemPrompt: systemPrompt,
                History: conversationHistory.Count > 0 ? conversationHistory : null,
                Tools: toolDefinitions.Count > 0 ? toolDefinitions : null,
                Model: model);

            var response = await llm.CompleteAsync(request, cancellationToken);
            usage.Add(response);

            if (response.ToolCalls is not { Count: > 0 })
                return usage.ToResult(response.Text, iterations);

            conversationHistory.Add(new LlmMessage("assistant", response.Text, ToolCalls: response.ToolCalls));

            foreach (var toolCall in response.ToolCalls)
            {
                logger.LogInformation("Executing tool {ToolName}", toolCall.Name);
                var output = await toolRegistry.ExecuteAsync(toolCall, allowedToolNames, cancellationToken);
                conversationHistory.Add(new LlmMessage("tool", output, toolCall.Id));
            }

            userMessage = string.Empty;
        }

        logger.LogWarning("AgentLoop reached max iterations ({Max})", MaxIterations);

        var fallback = await llm.CompleteAsync(
            new LlmRequest(
                UserPrompt: "Resuma o que foi encontrado com base nos dados das ferramentas.",
                SystemPrompt: systemPrompt,
                History: conversationHistory,
                Model: model),
            cancellationToken);

        usage.Add(fallback);
        return usage.ToResult(fallback.Text, iterations);
    }

    private sealed class UsageTotals
    {
        private int _total;
        private int _input;
        private int _output;
        private decimal _cost;
        private bool _costUnknown;

        public void Add(LlmResponse response)
        {
            _total += response.TotalTokens;
            _input += response.InputTokens;
            _output += response.OutputTokens;
            if (response.Cost is { } cost)
                _cost += cost;
            else
                _costUnknown = true;
        }

        public AgentResult ToResult(string reply, int iterations) =>
            new(reply, iterations, _total, _input, _output, _costUnknown ? null : _cost);
    }
}
