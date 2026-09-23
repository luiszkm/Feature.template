using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed class AgentLoop(
    ILlmService llm,
    ToolRegistry toolRegistry,
    IContentGuard contentGuard,
    IOptions<GuardrailOptions> guardrailOptions,
    ILogger<AgentLoop> logger,
    IOptions<LlmOptions>? llmOptions = null,
    IHostEnvironment? environment = null)
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
        var turnStart = conversationHistory.Count;
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
                History: conversationHistory.Count > 0 ? conversationHistory.ToArray() : null,
                Tools: toolDefinitions.Count > 0 ? toolDefinitions : null,
                Model: model);

            var response = await CompleteAsync(request, cancellationToken);
            usage.Add(response);

            if (response.ToolCalls is not { Count: > 0 })
                return usage.ToResult(await GuardReplyAsync(response.Text, cancellationToken), iterations, conversationHistory[turnStart..]);

            if (userMessage.Length > 0)
            {
                // From here on the message travels in the history, ahead of the turn it caused;
                // it is not a turn message - the caller already has it.
                conversationHistory.Add(new LlmMessage("user", userMessage));
                turnStart = conversationHistory.Count;
            }

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

        var fallback = await CompleteAsync(
            new LlmRequest(
                UserPrompt: "Resuma o que foi encontrado com base nos dados das ferramentas.",
                SystemPrompt: guardedSystemPrompt,
                History: conversationHistory.ToArray(),
                Model: model),
            cancellationToken);

        usage.Add(fallback);
        return usage.ToResult(await GuardReplyAsync(fallback.Text, cancellationToken), iterations, conversationHistory[turnStart..]);
    }

    /// <summary>One <c>chat {model}</c> span per model call, with the tokens the provider reported.</summary>
    private async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model ?? llmOptions?.Value.Model ?? string.Empty;
        using var activity = AiTelemetry.Source.StartActivity($"{AiTelemetry.Chat} {model}", ActivityKind.Client);
        activity?.SetTag(AiTelemetry.OperationName, AiTelemetry.Chat);
        activity?.SetTag(AiTelemetry.RequestModel, model);
        if (activity is not null && llmOptions is not null && environment is not null)
            activity.SetTag(AiTelemetry.ProviderName, AiTelemetry.ProviderValue(LlmServiceResolver.ProviderLabel(environment, llmOptions.Value)));
        try
        {
            var response = await llm.CompleteAsync(request, cancellationToken);
            activity?.SetTag(AiTelemetry.InputTokens, response.InputTokens);
            activity?.SetTag(AiTelemetry.OutputTokens, response.OutputTokens);
            return response;
        }
        catch (Exception ex)
        {
            AiTelemetry.RecordError(activity, ex);
            throw;
        }
    }

    /// <summary>Whatever a tool returns reaches the model as delimited data, never as an exception.</summary>
    private async Task<string> ExecuteToolAsync(
        ToolCall toolCall,
        IReadOnlyList<string>? allowedToolNames,
        CancellationToken cancellationToken)
    {
        using var activity = AiTelemetry.Source.StartActivity($"{AiTelemetry.ExecuteTool} {toolCall.Name}");
        activity?.SetTag(AiTelemetry.OperationName, AiTelemetry.ExecuteTool);
        activity?.SetTag(AiTelemetry.ToolName, toolCall.Name);
        activity?.SetTag(AiTelemetry.ToolCallId, toolCall.Id);

        var allowed = allowedToolNames is null || allowedToolNames.Contains(toolCall.Name, StringComparer.Ordinal);
        if (!allowed || !toolRegistry.IsKnown(toolCall.Name))
            AiTelemetry.RecordError(activity, AiTelemetry.ToolNotFound);

        string output;
        try
        {
            output = await toolRegistry.ExecuteAsync(toolCall, allowedToolNames, cancellationToken);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            AiTelemetry.RecordError(activity, ex);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            AiTelemetry.RecordError(activity, ex);
            logger.LogWarning("Tool {ToolName} denied: current user lacks permission", toolCall.Name);
            return AgentGuardrails.Wrap(AgentGuardrails.ToolError(AgentGuardrails.PermissionDenied, toolCall.Name));
        }
        catch (Exception ex)
        {
            AiTelemetry.RecordError(activity, ex);
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

        public AgentResult ToResult(string reply, int iterations, IReadOnlyList<LlmMessage> turnMessages) =>
            new(reply, iterations, _total, _input, _output, _costUnknown ? null : _cost, turnMessages);
    }
}
