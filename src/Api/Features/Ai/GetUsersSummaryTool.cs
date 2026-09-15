using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Shared;

namespace Api.Features.Ai;

public sealed class GetUsersSummaryTool(
    IUserDirectory userDirectory,
    ICurrentUserAccessor currentUser) : ITool
{
    public ToolDefinition Definition { get; } = new(
        Name: "get_users_summary",
        Description: "Returns a summary of registered users in the current tenant.",
        InputSchema: new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["page_size"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "Number of users to retrieve (default: 10, max: 50)"
                }
            },
            ["required"] = new JsonArray()
        });

    public async Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsurePermission(currentUser, "identity.user.read");

        var pageSize = toolCall.Parameters["page_size"]?.GetValue<int>() ?? 10;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var result = await userDirectory.ListAsync(
            new ListQuery(PageNumber: 1, PageSize: pageSize),
            cancellationToken);

        var summary = new
        {
            total_count = result.TotalCount,
            page_size = result.PageSize,
            users = result.Data.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                name = $"{u.FirstName} {u.LastName}".Trim()
            })
        };

        return JsonSerializer.Serialize(summary);
    }
}
