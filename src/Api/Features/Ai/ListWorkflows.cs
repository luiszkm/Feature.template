using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListWorkflowsQuery(
    int PageNumber = 1,
    int PageSize = 20)
    : ListQuery(PageNumber, PageSize),
        IQuery<PaginatedListOutput<WorkflowSummaryOutput>>;

public sealed class ListWorkflowsQueryValidator : ListQueryValidator<ListWorkflowsQuery>;

public sealed class ListWorkflowsHandler(IWorkflowRepository workflows)
    : IRequestHandler<ListWorkflowsQuery, PaginatedListOutput<WorkflowSummaryOutput>>
{
    public async Task<PaginatedListOutput<WorkflowSummaryOutput>> Handle(
        ListWorkflowsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await workflows.ListActiveAsync(request, cancellationToken);
        return new PaginatedListOutput<WorkflowSummaryOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(WorkflowMapper.ToSummary).ToList());
    }
}

public sealed class ListWorkflowsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/workflows", async (
            [AsParameters] ListWorkflowsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListWorkflows")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<PaginatedListOutput<WorkflowSummaryOutput>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
