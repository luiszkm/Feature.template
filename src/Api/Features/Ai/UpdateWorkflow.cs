using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record UpdateWorkflowCommand(
    Guid WorkflowId,
    string Name,
    string? Description,
    IReadOnlyList<WorkflowNodeInput>? Nodes,
    IReadOnlyList<WorkflowEdgeInput>? Edges) : ICommand<WorkflowOutput>, IWorkflowDefinition;

public sealed class UpdateWorkflowValidator(IAgentRepository agents)
    : WorkflowDefinitionValidator<UpdateWorkflowCommand>(agents);

public sealed class UpdateWorkflowHandler(
    IWorkflowRepository workflows,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateWorkflowCommand, WorkflowOutput>
{
    public async Task<WorkflowOutput> Handle(UpdateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await workflows.GetByIdAsync(request.WorkflowId, cancellationToken)
            ?? throw new NotFoundException($"Workflow '{request.WorkflowId}' was not found.");

        workflow.Replace(
            request.Name,
            request.Description,
            WorkflowMapper.ToNodes(request.Nodes),
            WorkflowMapper.ToEdges(request.Edges));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkflowMapper.ToOutput(workflow);
    }
}

public sealed class UpdateWorkflowEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/ai/workflows/{workflowId:guid}", async (
            Guid workflowId,
            UpdateWorkflowRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new UpdateWorkflowCommand(workflowId, body.Name, body.Description, body.Nodes, body.Edges),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("UpdateWorkflow")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces<WorkflowOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateWorkflowRequest(
    string Name,
    string? Description,
    IReadOnlyList<WorkflowNodeInput>? Nodes,
    IReadOnlyList<WorkflowEdgeInput>? Edges);
