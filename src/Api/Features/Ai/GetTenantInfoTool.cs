using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Features.Tenants;
using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed class GetTenantInfoTool(
    IMediator mediator,
    ICurrentUserAccessor currentUser) : ITool
{
    public ToolDefinition Definition { get; } = new(
        Name: "get_tenant_info",
        Description: "Returns information about tenants configured in the system.",
        InputSchema: new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["page_size"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "Number of tenants to retrieve (default: 20)"
                }
            },
            ["required"] = new JsonArray()
        });

    public async Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsurePermission(currentUser, TenantsPermissions.Read);

        var pageSize = toolCall.Parameters["page_size"]?.GetValue<int>() ?? 20;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await mediator.Send(new ListTenantsQuery(PageNumber: 1, PageSize: pageSize), cancellationToken);

        var summary = new
        {
            total_count = result.TotalCount,
            tenants = result.Data.Select(t => new
            {
                tenant_id = t.TenantId,
                tenant_key = t.TenantKey,
                isolation_mode = t.IsolationMode,
                is_active = t.IsActive
            })
        };

        return JsonSerializer.Serialize(summary);
    }
}
