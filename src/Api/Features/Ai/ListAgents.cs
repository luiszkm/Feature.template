using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListAgentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null)
    : ListQuery(PageNumber, PageSize, SearchTerm, SortBy, SortDirection),
        IQuery<PaginatedListOutput<AgentOutput>>;

public sealed class ListAgentsQueryValidator : ListQueryValidator<ListAgentsQuery>;

public sealed class ListAgentsHandler(IAgentRepository agents)
    : IRequestHandler<ListAgentsQuery, PaginatedListOutput<AgentOutput>>
{
    public async Task<PaginatedListOutput<AgentOutput>> Handle(
        ListAgentsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await agents.ListAsync(request, cancellationToken);
        return new PaginatedListOutput<AgentOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(AgentMapper.ToOutput).ToList());
    }
}

public sealed class ListAgentsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/agents", async (
            [AsParameters] ListAgentsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListAgents")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<PaginatedListOutput<AgentOutput>>(StatusCodes.Status200OK);
    }
}
