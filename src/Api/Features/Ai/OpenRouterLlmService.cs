using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

internal sealed class OpenRouterLlmService(
    IHttpClientFactory httpClientFactory,
    IOptions<LlmOptions> options) : ILlmService
{
    public const string HttpClientName = "OpenRouterLlm";
    public const string DefaultBaseUrl = "https://openrouter.ai/api/v1";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var payload = new OpenRouterRequest(
            request.Model ?? options.Value.Model,
            BuildMessages(request),
            request.Temperature,
            request.Tools is { Count: > 0 } ? request.Tools.Select(ToTool).ToList() : null);

        using var response = await client.PostAsJsonAsync("chat/completions", payload, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(Json, cancellationToken)
            ?? throw new InvalidOperationException("OpenRouter returned an empty response.");

        var message = body.Choices?.FirstOrDefault()?.Message;
        var toolCalls = message?.ToolCalls?
            .Select(call => new ToolCall(
                call.Id,
                call.Function.Name,
                ParseArgs(call.Function.Arguments)))
            .ToList();

        return new LlmResponse(
            message?.Content ?? string.Empty,
            body.Usage?.TotalTokens ?? 0,
            toolCalls is { Count: > 0 } ? toolCalls : null,
            InputTokens: body.Usage?.PromptTokens ?? 0,
            OutputTokens: body.Usage?.CompletionTokens ?? 0,
            Cost: body.Usage?.Cost);
    }

    internal static List<OpenRouterMessage> BuildMessages(LlmRequest request)
    {
        var messages = new List<OpenRouterMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new OpenRouterMessage("system", request.SystemPrompt));

        if (request.History is not null)
        {
            foreach (var item in request.History)
                messages.Add(FromHistory(item));
        }

        if (!string.IsNullOrWhiteSpace(request.UserPrompt))
            messages.Add(new OpenRouterMessage("user", request.UserPrompt));

        return messages;
    }

    private static OpenRouterMessage FromHistory(LlmMessage item)
    {
        if (item.ToolCalls is { Count: > 0 })
        {
            return new OpenRouterMessage(
                item.Role,
                string.IsNullOrEmpty(item.Content) ? null : item.Content,
                ToolCalls: item.ToolCalls.Select(call => new OpenRouterToolCall(
                    call.Id,
                    "function",
                    new OpenRouterFunction(call.Name, call.Parameters.ToJsonString()))).ToList());
        }

        return new OpenRouterMessage(item.Role, item.Content, item.ToolCallId);
    }

    private static OpenRouterTool ToTool(ToolDefinition tool) =>
        new("function", new OpenRouterFunctionSpec(tool.Name, tool.Description, tool.InputSchema));

    private static JsonObject ParseArgs(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        return JsonNode.Parse(arguments) as JsonObject ?? [];
    }

    internal sealed record OpenRouterRequest(
        string Model,
        IReadOnlyList<OpenRouterMessage> Messages,
        float Temperature,
        IReadOnlyList<OpenRouterTool>? Tools);

    internal sealed record OpenRouterMessage(
        string Role,
        string? Content,
        string? ToolCallId = null,
        IReadOnlyList<OpenRouterToolCall>? ToolCalls = null);

    internal sealed record OpenRouterTool(string Type, OpenRouterFunctionSpec Function);

    internal sealed record OpenRouterFunctionSpec(string Name, string Description, JsonObject Parameters);

    internal sealed record OpenRouterToolCall(string Id, string Type, OpenRouterFunction Function);

    internal sealed record OpenRouterFunction(string Name, string Arguments);

    private sealed record OpenRouterResponse(
        IReadOnlyList<OpenRouterChoice>? Choices,
        OpenRouterUsage? Usage);

    private sealed record OpenRouterChoice(OpenRouterMessage? Message);

    private sealed record OpenRouterUsage(
        [property: JsonPropertyName("total_tokens")] int TotalTokens,
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens = 0,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens = 0,
        [property: JsonPropertyName("cost")] decimal? Cost = null);
}
