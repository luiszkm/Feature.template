using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetAgentFileQuery(Guid AgentId, Guid FileId) : IQuery<AgentFileOutput>;

public sealed class GetAgentFileHandler(
    IAgentRepository agents,
    IAgentFileRepository files) : IRequestHandler<GetAgentFileQuery, AgentFileOutput>
{
    public async Task<AgentFileOutput> Handle(GetAgentFileQuery request, CancellationToken cancellationToken)
    {
        _ = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        var file = await files.GetByIdAsync(request.FileId, cancellationToken);
        if (file is null || file.AgentId != request.AgentId)
            throw new NotFoundException($"Agent file '{request.FileId}' was not found.");

        return AgentMapper.ToOutput(file, includeContent: true);
    }
}

public sealed class GetAgentFileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/agents/{agentId:guid}/files/{fileId:guid}", async (
            Guid agentId,
            Guid fileId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetAgentFileQuery(agentId, fileId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetAgentFile")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<AgentFileOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
