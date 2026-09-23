using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetWorkflowQuery(Guid WorkflowId) : IQuery<WorkflowOutput>;

public sealed class GetWorkflowHandler(IWorkflowRepository workflows)
    : IRequestHandler<GetWorkflowQuery, WorkflowOutput>
{
    public async Task<WorkflowOutput> Handle(GetWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflow = await workflows.GetByIdAsync(request.WorkflowId, cancellationToken)
            ?? throw new NotFoundException($"Workflow '{request.WorkflowId}' was not found.");

        return WorkflowMapper.ToOutput(workflow);
    }
}

public sealed class GetWorkflowEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/workflows/{workflowId:guid}", async (
            Guid workflowId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetWorkflowQuery(workflowId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetWorkflow")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<WorkflowOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
