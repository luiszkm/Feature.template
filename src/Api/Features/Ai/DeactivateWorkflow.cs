using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record DeactivateWorkflowCommand(Guid WorkflowId) : ICommand<bool>;

public sealed class DeactivateWorkflowHandler(
    IWorkflowRepository workflows,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateWorkflowCommand, bool>
{
    public async Task<bool> Handle(DeactivateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await workflows.GetByIdAsync(request.WorkflowId, cancellationToken)
            ?? throw new NotFoundException($"Workflow '{request.WorkflowId}' was not found.");

        workflow.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class DeactivateWorkflowEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/ai/workflows/{workflowId:guid}", async (
            Guid workflowId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeactivateWorkflowCommand(workflowId), cancellationToken);
            return Results.NoContent();
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("DeactivateWorkflow")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
