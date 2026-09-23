using System.Diagnostics;

namespace Api.Features.Ai;

/// <summary>
/// Spans of the agent loop in the OpenTelemetry GenAI vocabulary. Content attributes
/// (<c>gen_ai.input.messages</c>, tool arguments and results) are never recorded: they are tenant data.
/// </summary>
public static class AiTelemetry
{
    public const string ActivitySourceName = "Api.Features.Ai";

    public const string OperationName = "gen_ai.operation.name";
    public const string ProviderName = "gen_ai.provider.name";
    public const string RequestModel = "gen_ai.request.model";
    public const string AgentId = "gen_ai.agent.id";
    public const string AgentName = "gen_ai.agent.name";
    public const string ConversationId = "gen_ai.conversation.id";
    public const string InputTokens = "gen_ai.usage.input_tokens";
    public const string OutputTokens = "gen_ai.usage.output_tokens";
    public const string ToolName = "gen_ai.tool.name";
    public const string ToolCallId = "gen_ai.tool.call.id";
    public const string ErrorType = "error.type";

    public const string InvokeAgent = "invoke_agent";
    public const string Chat = "chat";
    public const string ExecuteTool = "execute_tool";
    public const string ToolNotFound = "tool_not_found";

    internal static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>Well-known <c>gen_ai.provider.name</c> values; the Agent Framework endpoint is configurable, so not <c>openai</c>.</summary>
    public static string ProviderValue(string providerLabel) => providerLabel switch
    {
        LlmProviders.OpenRouter => "openrouter",
        LlmProviders.MicrosoftAgentFramework => "microsoft.agent_framework",
        _ => providerLabel.ToLowerInvariant()
    };

    internal static void RecordError(Activity? activity, Exception exception) => RecordError(activity, exception.GetType().Name);

    internal static void RecordError(Activity? activity, string errorType)
    {
        if (activity is null)
            return;

        activity.SetTag(ErrorType, errorType);
        activity.SetStatus(ActivityStatusCode.Error);
    }
}
