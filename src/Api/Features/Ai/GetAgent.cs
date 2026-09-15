using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetAgentQuery(Guid AgentId) : IQuery<AgentOutput>;

public sealed class GetAgentHandler(IAgentRepository agents)
    : IRequestHandler<GetAgentQuery, AgentOutput>
{
    public async Task<AgentOutput> Handle(GetAgentQuery request, CancellationToken cancellationToken)
    {
        var agent = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        return AgentMapper.ToOutput(agent);
    }
}

public sealed class GetAgentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/agents/{agentId:guid}", async (
            Guid agentId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetAgentQuery(agentId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetAgent")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<AgentOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
