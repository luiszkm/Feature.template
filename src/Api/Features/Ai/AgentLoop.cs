using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed class AgentLoop(
    ILlmService llm,
    ToolRegistry toolRegistry,
    IContentGuard contentGuard,
    IOptions<GuardrailOptions> guardrailOptions,
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
        var guardedSystemPrompt = AgentGuardrails.WithSuffix(systemPrompt);
        var iterations = 0;
        var usage = new UsageTotals();

        while (iterations < MaxIterations)
        {
            iterations++;

            var request = new LlmRequest(
                UserPrompt: userMessage,
                SystemPrompt: guardedSystemPrompt,
                History: conversationHistory.Count > 0 ? conversationHistory : null,
                Tools: toolDefinitions.Count > 0 ? toolDefinitions : null,
                Model: model);

            var response = await llm.CompleteAsync(request, cancellationToken);
            usage.Add(response);

            if (response.ToolCalls is not { Count: > 0 })
                return usage.ToResult(await GuardReplyAsync(response.Text, cancellationToken), iterations);

            conversationHistory.Add(new LlmMessage("assistant", response.Text, ToolCalls: response.ToolCalls));

            foreach (var toolCall in response.ToolCalls)
            {
                logger.LogInformation("Executing tool {ToolName}", toolCall.Name);
                var output = await ExecuteToolAsync(toolCall, allowedToolNames, cancellationToken);
                conversationHistory.Add(new LlmMessage("tool", output, toolCall.Id));
            }

            userMessage = string.Empty;
        }

        logger.LogWarning("AgentLoop reached max iterations ({Max})", MaxIterations);

        var fallback = await llm.CompleteAsync(
            new LlmRequest(
                UserPrompt: "Resuma o que foi encontrado com base nos dados das ferramentas.",
                SystemPrompt: guardedSystemPrompt,
                History: conversationHistory,
                Model: model),
            cancellationToken);

        usage.Add(fallback);
        return usage.ToResult(await GuardReplyAsync(fallback.Text, cancellationToken), iterations);
    }

    /// <summary>Whatever a tool returns reaches the model as delimited data, never as an exception.</summary>
    private async Task<string> ExecuteToolAsync(
        ToolCall toolCall,
        IReadOnlyList<string>? allowedToolNames,
        CancellationToken cancellationToken)
    {
        string output;
        try
        {
            output = await toolRegistry.ExecuteAsync(toolCall, allowedToolNames, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Tool {ToolName} denied: current user lacks permission", toolCall.Name);
            return AgentGuardrails.Wrap(AgentGuardrails.ToolError(AgentGuardrails.PermissionDenied, toolCall.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool {ToolName} failed", toolCall.Name);
            return AgentGuardrails.Wrap(AgentGuardrails.ToolError(AgentGuardrails.ToolFailed, toolCall.Name));
        }

        output = AgentGuardrails.Truncate(output, guardrailOptions.Value.MaxToolOutputChars);

        var verdict = await contentGuard.EvaluateAsync(new GuardInput(GuardSubject.ToolOutput, output), cancellationToken);
        if (verdict.Blocked)
        {
            logger.LogWarning("Tool {ToolName} output blocked by content guard", toolCall.Name);
            output = AgentGuardrails.ToolError(AgentGuardrails.ToolOutputBlocked, toolCall.Name);
        }

        return AgentGuardrails.Wrap(output);
    }

    private async Task<string> GuardReplyAsync(string reply, CancellationToken cancellationToken)
    {
        var verdict = await contentGuard.EvaluateAsync(new GuardInput(GuardSubject.Reply, reply), cancellationToken);
        if (!verdict.Blocked)
            return reply;

        logger.LogWarning("Agent reply held by content guard");
        return AgentGuardrails.HeldReply;
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
