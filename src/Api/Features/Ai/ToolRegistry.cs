namespace App.Features.Ai;

public sealed class ToolRegistry(IEnumerable<ITool> tools)
{
    private readonly IReadOnlyDictionary<string, ITool> _tools =
        tools.ToDictionary(t => t.Definition.Name);

    public IReadOnlyList<ToolDefinition> GetDefinitions() =>
        _tools.Values.Select(t => t.Definition).ToList();

    public Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken) =>
        _tools.TryGetValue(toolCall.Name, out var tool)
            ? tool.ExecuteAsync(toolCall, cancellationToken)
            : Task.FromResult($"{{\"error\":\"Tool '{toolCall.Name}' not found.\"}}");
}
