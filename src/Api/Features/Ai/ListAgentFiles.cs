using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListAgentFilesQuery(Guid AgentId) : IQuery<IReadOnlyList<AgentFileOutput>>;

public sealed class ListAgentFilesHandler(
    IAgentRepository agents,
    IAgentFileRepository files) : IRequestHandler<ListAgentFilesQuery, IReadOnlyList<AgentFileOutput>>
{
    public async Task<IReadOnlyList<AgentFileOutput>> Handle(
        ListAgentFilesQuery request,
        CancellationToken cancellationToken)
    {
        _ = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        var list = await files.ListByAgentAsync(request.AgentId, cancellationToken);
        return list.Select(file => AgentMapper.ToOutput(file, includeContent: false)).ToList();
    }
}

public sealed class ListAgentFilesEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/agents/{agentId:guid}/files", async (
            Guid agentId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new ListAgentFilesQuery(agentId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListAgentFiles")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<IReadOnlyList<AgentFileOutput>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
