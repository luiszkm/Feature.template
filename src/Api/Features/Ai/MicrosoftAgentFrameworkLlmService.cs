using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Api.Features.Ai;

internal sealed class MicrosoftAgentFrameworkLlmService : ILlmService
{
    public const string HttpClientName = "MicrosoftAgentFrameworkLlm";

    private readonly IChatClient _chatClient;

    public MicrosoftAgentFrameworkLlmService(IHttpClientFactory httpClientFactory, IOptions<LlmOptions> options)
    {
        var llm = options.Value;
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var openAi = new OpenAIClient(
            new ApiKeyCredential(llm.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(LlmServiceResolver.EffectiveBaseUrl(llm) + "/"),
                Transport = new HttpClientPipelineTransport(httpClient)
            });

        IChatClient inner = openAi.GetChatClient(llm.Model).AsIChatClient();
        // ChatClientAgent is the Microsoft Agent Framework wrapper around IChatClient.
        // Function invocation stays in AgentLoop: tools are advertised as declarations only.
        _ = new ChatClientAgent(inner, name: "product-template-llm");
        _chatClient = inner;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var messages = MapMessages(request);
        var chatOptions = new ChatOptions
        {
            Instructions = request.SystemPrompt,
            Temperature = request.Temperature,
            Tools = request.Tools is { Count: > 0 } ? request.Tools.Select(ToDeclaration).ToList() : null
        };

        var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        var toolCalls = response.Messages
            .SelectMany(message => message.Contents.OfType<FunctionCallContent>())
            .Select(call => new ToolCall(call.CallId, call.Name, ToJson(call.Arguments)))
            .ToList();

        return new LlmResponse(
            response.Text ?? string.Empty,
            (int)(response.Usage?.TotalTokenCount ?? 0),
            toolCalls.Count > 0 ? toolCalls : null);
    }

    internal static List<ChatMessage> MapMessages(LlmRequest request)
    {
        var messages = new List<ChatMessage>();
        if (request.History is not null)
        {
            foreach (var item in request.History)
                messages.Add(FromHistory(item));
        }

        if (!string.IsNullOrWhiteSpace(request.UserPrompt))
            messages.Add(new ChatMessage(ChatRole.User, request.UserPrompt));

        return messages;
    }

    private static ChatMessage FromHistory(LlmMessage item)
    {
        if (item.ToolCalls is { Count: > 0 })
        {
            var contents = item.ToolCalls
                .Select(call => (AIContent)new FunctionCallContent(call.Id, call.Name, ToDictionary(call.Parameters)))
                .ToList();
            if (!string.IsNullOrEmpty(item.Content))
                contents.Insert(0, new TextContent(item.Content));
            return new ChatMessage(ChatRole.Assistant, contents);
        }

        if (string.Equals(item.Role, "tool", StringComparison.OrdinalIgnoreCase)
            && item.ToolCallId is not null)
        {
            return new ChatMessage(ChatRole.Tool, [new FunctionResultContent(item.ToolCallId, item.Content)]);
        }

        var role = item.Role.ToLowerInvariant() switch
        {
            "assistant" => ChatRole.Assistant,
            "system" => ChatRole.System,
            _ => ChatRole.User
        };
        return new ChatMessage(role, item.Content);
    }

    private static AITool ToDeclaration(ToolDefinition tool)
    {
        using var document = JsonDocument.Parse(tool.InputSchema.ToJsonString());
        return AIFunctionFactory.CreateDeclaration(tool.Name, tool.Description, document.RootElement.Clone());
    }

    private static JsonObject ToJson(IDictionary<string, object?>? arguments)
    {
        var node = new JsonObject();
        if (arguments is null)
            return node;

        foreach (var (key, value) in arguments)
            node[key] = value is null ? null : JsonSerializer.SerializeToNode(value);

        return node;
    }

    private static Dictionary<string, object?> ToDictionary(JsonObject parameters)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in parameters)
            result[key] = value;
        return result;
    }
}
