using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Shared;

namespace Api.Features.Ai;

public sealed class ListAgentFilesTool(
    IAgentFileRepository files,
    IAgentRuntimeContext runtime) : ITool
{
    public ToolDefinition Definition { get; } = new(
        Name: AgentToolNames.ListAgentFiles,
        Description: "Lists text files attached to the current agent.",
        InputSchema: new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
            ["required"] = new JsonArray()
        });

    public async Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        if (runtime.AgentId is not { } agentId)
            return """{"error":"No agent is bound to this chat."}""";

        var list = await files.ListByAgentAsync(agentId, cancellationToken);
        var payload = list.Select(file => new { file_id = file.Id, name = file.Name });
        return JsonSerializer.Serialize(new { files = payload });
    }
}

public sealed class ReadAgentFileTool(
    IAgentFileRepository files,
    IAgentRuntimeContext runtime) : ITool
{
    public ToolDefinition Definition { get; } = new(
        Name: AgentToolNames.ReadAgentFile,
        Description: "Reads the text content of a file attached to the current agent.",
        InputSchema: new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["file_id"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The file id returned by list_agent_files."
                }
            },
            ["required"] = new JsonArray("file_id")
        });

    public async Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        if (runtime.AgentId is not { } agentId)
            return """{"error":"No agent is bound to this chat."}""";

        var raw = toolCall.Parameters["file_id"]?.GetValue<string>();
        if (!Guid.TryParse(raw, out var fileId))
            return """{"error":"file_id is required."}""";

        var file = await files.GetByIdAsync(fileId, cancellationToken);
        if (file is null || file.AgentId != agentId)
            return """{"error":"File not found for this agent."}""";

        return JsonSerializer.Serialize(new { file_id = file.Id, name = file.Name, content = file.Content });
    }
}
