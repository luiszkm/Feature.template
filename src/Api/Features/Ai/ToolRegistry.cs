namespace Api.Features.Ai;

public sealed class ToolRegistry(IEnumerable<ITool> tools)
{
    private readonly IReadOnlyDictionary<string, ITool> _tools =
        tools.ToDictionary(t => t.Definition.Name);

    public bool IsKnown(string name) => _tools.ContainsKey(name);

    public IReadOnlyList<ToolDefinition> GetDefinitions(IReadOnlyCollection<string>? allowlist = null)
    {
        var values = _tools.Values.Select(t => t.Definition);
        if (allowlist is null)
            return values.ToList();

        var allowed = allowlist.ToHashSet(StringComparer.Ordinal);
        return values.Where(d => allowed.Contains(d.Name)).ToList();
    }

    public Task<string> ExecuteAsync(
        ToolCall toolCall,
        IReadOnlyCollection<string>? allowlist = null,
        CancellationToken cancellationToken = default)
    {
        if (allowlist is not null
            && !allowlist.Contains(toolCall.Name, StringComparer.Ordinal))
        {
            return Task.FromResult(AgentGuardrails.ToolError(AgentGuardrails.ToolNotFound, toolCall.Name));
        }

        return _tools.TryGetValue(toolCall.Name, out var tool)
            ? tool.ExecuteAsync(toolCall, cancellationToken)
            : Task.FromResult(AgentGuardrails.ToolError(AgentGuardrails.ToolNotFound, toolCall.Name));
    }
}
