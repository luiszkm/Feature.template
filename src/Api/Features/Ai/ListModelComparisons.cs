using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListModelComparisonsQuery(
    int PageNumber = 1,
    int PageSize = 20)
    : ListQuery(PageNumber, PageSize),
        IQuery<PaginatedListOutput<ComparisonSummaryOutput>>;

public sealed class ListModelComparisonsQueryValidator : ListQueryValidator<ListModelComparisonsQuery>;

public sealed class ListModelComparisonsHandler(
    IModelComparisonRepository comparisons,
    IAgentRepository agents)
    : IRequestHandler<ListModelComparisonsQuery, PaginatedListOutput<ComparisonSummaryOutput>>
{
    public async Task<PaginatedListOutput<ComparisonSummaryOutput>> Handle(
        ListModelComparisonsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await comparisons.ListAsync(request, cancellationToken);

        var names = new Dictionary<Guid, string>();
        foreach (var agentId in page.Data.Select(c => c.AgentId).Distinct())
        {
            var agent = await agents.GetByIdAsync(agentId, cancellationToken);
            names[agentId] = agent?.Name ?? string.Empty;
        }

        return new PaginatedListOutput<ComparisonSummaryOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(c => ComparisonMapper.ToSummary(c, names[c.AgentId])).ToList());
    }
}

public sealed class ListModelComparisonsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/comparisons", async (
            [AsParameters] ListModelComparisonsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListModelComparisons")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<PaginatedListOutput<ComparisonSummaryOutput>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
