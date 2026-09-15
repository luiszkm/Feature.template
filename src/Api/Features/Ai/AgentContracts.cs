namespace Api.Features.Ai;

public sealed record AgentOutput(
    Guid AgentId,
    string Name,
    string Instructions,
    IReadOnlyList<string> ToolNames,
    bool IsActive,
    bool IsDefault,
    DateTime CreatedAt);

public sealed record AgentFileOutput(Guid FileId, string Name, string? Content = null);

public static class AgentMapper
{
    public static AgentOutput ToOutput(Agent agent) =>
        new(
            agent.Id,
            agent.Name,
            agent.Instructions,
            agent.ToolNames,
            agent.IsActive,
            agent.IsDefault,
            agent.CreatedAt);

    public static AgentFileOutput ToOutput(AgentFile file, bool includeContent) =>
        new(file.Id, file.Name, includeContent ? file.Content : null);
}

public static class AgentToolNames
{
    public const string GetUsersSummary = "get_users_summary";
    public const string GetTenantInfo = "get_tenant_info";
    public const string ListAgentFiles = "list_agent_files";
    public const string ReadAgentFile = "read_agent_file";

    public static IReadOnlyList<string> DefaultSeed { get; } =
        [GetUsersSummary, GetTenantInfo];
}

public static class LlmProviders
{
    public const string MicrosoftAgentFramework = "MicrosoftAgentFramework";
    public const string OpenRouter = "OpenRouter";

    public static bool IsKnown(string? provider) =>
        string.Equals(provider, MicrosoftAgentFramework, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, OpenRouter, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? provider) =>
        string.Equals(provider, MicrosoftAgentFramework, StringComparison.OrdinalIgnoreCase)
            ? MicrosoftAgentFramework
            : OpenRouter;
}

public sealed class LlmOptions
{
    public const string SectionName = "Ai:Llm";

    public string Provider { get; set; } = LlmProviders.OpenRouter;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "openai/gpt-4o-mini";
    public string BaseUrl { get; set; } = string.Empty;
}

public interface IAgentRuntimeContext
{
    Guid? AgentId { get; }
    void Set(Guid agentId);
}

internal sealed class AgentRuntimeContext : IAgentRuntimeContext
{
    public Guid? AgentId { get; private set; }

    public void Set(Guid agentId) => AgentId = agentId;
}
