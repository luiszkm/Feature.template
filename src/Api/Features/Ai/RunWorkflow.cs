using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record RunWorkflowCommand(Guid WorkflowId, string Input) : ICommand<WorkflowRunOutput>;

public sealed class RunWorkflowValidator : AbstractValidator<RunWorkflowCommand>
{
    public const int MaxInputLength = 4000;

    public RunWorkflowValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.Input).NotEmpty().MaximumLength(MaxInputLength).OverridePropertyName("input");
    }
}

/// <summary>Accepts the run and returns; <see cref="WorkflowRunner"/> executes it in the background.</summary>
public sealed class RunWorkflowHandler(
    IWorkflowRepository workflows,
    IWorkflowRunRepository runs,
    IContentGuard contentGuard,
    AiQuota quota,
    ITenantContext tenantContext,
    ICurrentUserAccessor currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<RunWorkflowCommand, WorkflowRunOutput>
{
    public async Task<WorkflowRunOutput> Handle(RunWorkflowCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before running a workflow.");

        var workflow = await workflows.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null || !workflow.IsActive)
            throw new NotFoundException($"Workflow '{request.WorkflowId}' was not found.");

        await quota.EnsureWithinAsync(cancellationToken);
        await contentGuard.EnsureMessageAllowedAsync(request.Input, cancellationToken);

        var run = WorkflowRun.Create(tenantId, workflow, request.Input, RunPrincipal.From(currentUser.User));
        await runs.AddAsync(run, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkflowRunMapper.ToOutput(run);
    }
}

public sealed class RunWorkflowEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ai/workflows/{workflowId:guid}/runs", async (
            Guid workflowId,
            RunWorkflowRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new RunWorkflowCommand(workflowId, body.Input), cancellationToken);
            return Results.Accepted($"/api/v1/ai/workflows/{workflowId}/runs/{result.RunId}", result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("RunWorkflow")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .RequireRateLimiting(RateLimitPolicies.AiRateLimitPolicy)
        .Produces<WorkflowRunOutput>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}

public sealed record RunWorkflowRequest(string Input);

public sealed record WorkflowRunStepOutput(
    string NodeKey,
    Guid AgentId,
    string Status,
    string? Output,
    int InputTokens,
    int OutputTokens,
    decimal? Cost,
    long LatencyMs,
    int IterationsUsed,
    string? ErrorCode,
    DateTime? StartedAt,
    DateTime? FinishedAt);

public sealed record WorkflowRunOutput(
    Guid RunId,
    Guid WorkflowId,
    string Status,
    string Input,
    string? ErrorCode,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    Guid? CreatedByUserId,
    decimal? TotalCost,
    IReadOnlyList<WorkflowNodeOutput> Nodes,
    IReadOnlyList<WorkflowEdgeOutput> Edges,
    IReadOnlyList<WorkflowRunStepOutput> Steps);

public sealed record WorkflowRunSummaryOutput(
    Guid RunId,
    string Status,
    string InputPreview,
    decimal? TotalCost,
    DateTime CreatedAt,
    DateTime? FinishedAt);

public static class WorkflowRunMapper
{
    public const int InputPreviewLength = 200;

    public static WorkflowRunOutput ToOutput(WorkflowRun run)
    {
        var graph = run.Graph;
        var order = graph.Nodes.Select((node, index) => (node.Key, index)).ToDictionary(p => p.Key, p => p.index);
        return new WorkflowRunOutput(
            run.Id,
            run.WorkflowId,
            run.Status.ToString(),
            run.Input,
            run.ErrorCode,
            run.CreatedAt,
            run.StartedAt,
            run.FinishedAt,
            run.CreatedByUserId,
            run.TotalCost,
            graph.Nodes.Select(n => new WorkflowNodeOutput(n.Key, n.AgentId, n.Instruction, n.X, n.Y)).ToList(),
            graph.Edges.Select(e => new WorkflowEdgeOutput(e.From, e.To)).ToList(),
            run.Steps
                .OrderBy(s => order.GetValueOrDefault(s.NodeKey, int.MaxValue))
                .Select(s => new WorkflowRunStepOutput(
                    s.NodeKey,
                    s.AgentId,
                    s.Status.ToString(),
                    s.Output,
                    s.InputTokens,
                    s.OutputTokens,
                    s.Cost,
                    s.LatencyMs,
                    s.IterationsUsed,
                    s.ErrorCode,
                    s.StartedAt,
                    s.FinishedAt))
                .ToList());
    }

    public static WorkflowRunSummaryOutput ToSummary(WorkflowRun run) =>
        new(
            run.Id,
            run.Status.ToString(),
            run.Input.Length <= InputPreviewLength ? run.Input : run.Input[..InputPreviewLength],
            run.TotalCost,
            run.CreatedAt,
            run.FinishedAt);
}
