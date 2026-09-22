using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetModelComparisonQuery(Guid ComparisonId) : IQuery<ComparisonOutput>;

public sealed class GetModelComparisonHandler(
    IModelComparisonRepository comparisons,
    IAgentRepository agents) : IRequestHandler<GetModelComparisonQuery, ComparisonOutput>
{
    public async Task<ComparisonOutput> Handle(GetModelComparisonQuery request, CancellationToken cancellationToken)
    {
        var comparison = await comparisons.GetByIdAsync(request.ComparisonId, cancellationToken)
            ?? throw new NotFoundException($"Comparison '{request.ComparisonId}' was not found.");

        var agent = await agents.GetByIdAsync(comparison.AgentId, cancellationToken);
        return ComparisonMapper.ToOutput(comparison, agent?.Name ?? string.Empty);
    }
}

public sealed class GetModelComparisonEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/comparisons/{comparisonId:guid}", async (
            Guid comparisonId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetModelComparisonQuery(comparisonId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetModelComparison")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<ComparisonOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
