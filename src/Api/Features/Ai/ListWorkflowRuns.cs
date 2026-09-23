using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListWorkflowRunsQuery(
    Guid WorkflowId,
    int PageNumber = 1,
    int PageSize = 20)
    : ListQuery(PageNumber, PageSize),
        IQuery<PaginatedListOutput<WorkflowRunSummaryOutput>>;

public sealed class ListWorkflowRunsQueryValidator : ListQueryValidator<ListWorkflowRunsQuery>;

public sealed class ListWorkflowRunsHandler(IWorkflowRepository workflows, IWorkflowRunRepository runs)
    : IRequestHandler<ListWorkflowRunsQuery, PaginatedListOutput<WorkflowRunSummaryOutput>>
{
    public async Task<PaginatedListOutput<WorkflowRunSummaryOutput>> Handle(
        ListWorkflowRunsQuery request,
        CancellationToken cancellationToken)
    {
        // Runs of a deactivated workflow stay readable; only an unknown one is 404.
        _ = await workflows.GetByIdAsync(request.WorkflowId, cancellationToken)
            ?? throw new NotFoundException($"Workflow '{request.WorkflowId}' was not found.");

        var page = await runs.ListAsync(request.WorkflowId, request, cancellationToken);
        return new PaginatedListOutput<WorkflowRunSummaryOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(WorkflowRunMapper.ToSummary).ToList());
    }
}

public sealed class ListWorkflowRunsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/workflows/{workflowId:guid}/runs", async (
            [AsParameters] ListWorkflowRunsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListWorkflowRuns")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<PaginatedListOutput<WorkflowRunSummaryOutput>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
