using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetWorkflowRunQuery(Guid WorkflowId, Guid RunId) : IQuery<WorkflowRunOutput>;

public sealed class GetWorkflowRunHandler(IWorkflowRunRepository runs)
    : IRequestHandler<GetWorkflowRunQuery, WorkflowRunOutput>
{
    public async Task<WorkflowRunOutput> Handle(GetWorkflowRunQuery request, CancellationToken cancellationToken)
    {
        var run = await runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null || run.WorkflowId != request.WorkflowId)
            throw new NotFoundException($"Workflow run '{request.RunId}' was not found.");

        return WorkflowRunMapper.ToOutput(run);
    }
}

public sealed class GetWorkflowRunEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/workflows/{workflowId:guid}/runs/{runId:guid}", async (
            Guid workflowId,
            Guid runId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetWorkflowRunQuery(workflowId, runId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetWorkflowRun")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<WorkflowRunOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
