using System.Text.Json.Nodes;

namespace App.Features.Ai;

public sealed record LlmRequest(
    string UserPrompt,
    string? SystemPrompt = null,
    float Temperature = 0.2f,
    IReadOnlyList<LlmMessage>? History = null,
    IReadOnlyList<ToolDefinition>? Tools = null);

public sealed record LlmMessage(string Role, string Content, string? ToolCallId = null);

public sealed record LlmResponse(
    string Text,
    int TotalTokens,
    IReadOnlyList<ToolCall>? ToolCalls = null);

public sealed record ToolDefinition(string Name, string Description, JsonObject InputSchema);

public sealed record ToolCall(string Id, string Name, JsonObject Parameters);

public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

public interface ITool
{
    ToolDefinition Definition { get; }
    Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default);
}

public sealed record AiUsageRecord(
    string Service,
    string Provider,
    string Model,
    string Module,
    string Operation,
    Guid TenantId,
    int? TokensUsed,
    TimeSpan Latency,
    bool Success,
    string? ErrorCode);

public interface IAiUsageTracker
{
    Task TrackAsync(AiUsageRecord record, CancellationToken cancellationToken = default);
}

internal sealed class NoOpAiUsageTracker : IAiUsageTracker
{
    public Task TrackAsync(AiUsageRecord record, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed record AgentResult(string Reply, int IterationsUsed, int TotalTokens);

public sealed record ChatAiOutput(string Reply, int IterationsUsed);
