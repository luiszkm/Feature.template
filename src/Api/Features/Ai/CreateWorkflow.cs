using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record WorkflowNodeInput(string Key, Guid AgentId, string? Instruction, double X, double Y);

public sealed record WorkflowEdgeInput(string From, string To);

public interface IWorkflowDefinition
{
    string Name { get; }
    string? Description { get; }
    IReadOnlyList<WorkflowNodeInput>? Nodes { get; }
    IReadOnlyList<WorkflowEdgeInput>? Edges { get; }
}

public sealed record CreateWorkflowCommand(
    string Name,
    string? Description,
    IReadOnlyList<WorkflowNodeInput>? Nodes,
    IReadOnlyList<WorkflowEdgeInput>? Edges) : ICommand<WorkflowOutput>, IWorkflowDefinition;

/// <summary>Shared by create and update: a workflow is a small DAG of active agents of the tenant.</summary>
public abstract class WorkflowDefinitionValidator<T> : AbstractValidator<T>
    where T : IWorkflowDefinition
{
    public const int MaxNodes = 10;
    public const int MaxEdges = 30;
    public const int MaxKeyLength = 50;
    public const int MaxInstructionLength = 2000;

    public const string CycleMessage = "As ligações formam um ciclo.";

    protected WorkflowDefinitionValidator(IAgentRepository agents)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).OverridePropertyName("name");
        RuleFor(x => x.Description).MaximumLength(1000).OverridePropertyName("description");
        RuleFor(x => x).CustomAsync(async (definition, context, cancellationToken) =>
        {
            var nodes = definition.Nodes ?? [];
            var edges = definition.Edges ?? [];

            if (nodes.Count is 0 or > MaxNodes)
                context.AddFailure("nodes", $"Um workflow tem entre 1 e {MaxNodes} nós.");

            var keysValid = true;
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (string.IsNullOrWhiteSpace(node.Key) || node.Key.Length > MaxKeyLength)
                {
                    context.AddFailure("nodes", $"Cada nó precisa de uma key com até {MaxKeyLength} caracteres.");
                    keysValid = false;
                }
                if (node.Instruction is { Length: > MaxInstructionLength })
                    context.AddFailure($"nodes[{index}].instruction", $"A instrução tem no máximo {MaxInstructionLength} caracteres.");
            }

            var keys = nodes.Select(n => n.Key).ToList();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
            {
                context.AddFailure("nodes", "Cada nó precisa de uma key única.");
                keysValid = false;
            }

            foreach (var agentId in nodes.Select(n => n.AgentId).Distinct())
            {
                var agent = await agents.GetByIdAsync(agentId, cancellationToken);
                if (agent is null || !agent.IsActive)
                    context.AddFailure("nodes", $"O agente '{agentId}' não existe ou está desativado.");
            }

            if (edges.Count > MaxEdges)
                context.AddFailure("edges", $"Um workflow tem no máximo {MaxEdges} ligações.");

            var known = keys.ToHashSet(StringComparer.Ordinal);
            if (edges.Any(e => !known.Contains(e.From) || !known.Contains(e.To)))
            {
                context.AddFailure("edges", "Uma ligação refere um nó que não existe.");
                return;
            }

            if (edges.Select(e => (e.From, e.To)).Distinct().Count() != edges.Count)
                context.AddFailure("edges", "Ligação repetida.");

            if (keysValid && WorkflowGraph.FindCycle(keys, edges.Select(e => (e.From, e.To)).ToList()) is not null)
                context.AddFailure("edges", CycleMessage);
        });
    }
}

public sealed class CreateWorkflowValidator(IAgentRepository agents)
    : WorkflowDefinitionValidator<CreateWorkflowCommand>(agents);

public sealed class CreateWorkflowHandler(
    IWorkflowRepository workflows,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateWorkflowCommand, WorkflowOutput>
{
    public async Task<WorkflowOutput> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before creating a workflow.");

        var workflow = Workflow.Create(
            tenantId,
            request.Name,
            request.Description,
            WorkflowMapper.ToNodes(request.Nodes),
            WorkflowMapper.ToEdges(request.Edges));

        await workflows.AddAsync(workflow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkflowMapper.ToOutput(workflow);
    }
}

public sealed class CreateWorkflowEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ai/workflows", async (
            CreateWorkflowCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/ai/workflows/{result.WorkflowId}", result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("CreateWorkflow")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces<WorkflowOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record WorkflowNodeOutput(string Key, Guid AgentId, string? Instruction, double X, double Y);

public sealed record WorkflowEdgeOutput(string From, string To);

public sealed record WorkflowOutput(
    Guid WorkflowId,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<WorkflowNodeOutput> Nodes,
    IReadOnlyList<WorkflowEdgeOutput> Edges,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record WorkflowSummaryOutput(
    Guid WorkflowId,
    string Name,
    int NodeCount,
    bool IsActive,
    DateTime UpdatedAt);

public static class WorkflowMapper
{
    public static IReadOnlyList<WorkflowNode> ToNodes(IReadOnlyList<WorkflowNodeInput>? nodes) =>
        (nodes ?? []).Select(n => new WorkflowNode(n.Key, n.AgentId, n.Instruction, n.X, n.Y)).ToList();

    public static IReadOnlyList<WorkflowEdge> ToEdges(IReadOnlyList<WorkflowEdgeInput>? edges) =>
        (edges ?? []).Select(e => new WorkflowEdge(e.From, e.To)).ToList();

    public static WorkflowOutput ToOutput(Workflow workflow) =>
        new(
            workflow.Id,
            workflow.Name,
            workflow.Description,
            workflow.IsActive,
            workflow.Nodes.Select(n => new WorkflowNodeOutput(n.Key, n.AgentId, n.Instruction, n.X, n.Y)).ToList(),
            workflow.Edges.Select(e => new WorkflowEdgeOutput(e.FromKey, e.ToKey)).ToList(),
            workflow.CreatedAt,
            workflow.UpdatedAt);

    public static WorkflowSummaryOutput ToSummary(Workflow workflow) =>
        new(workflow.Id, workflow.Name, workflow.Nodes.Count, workflow.IsActive, workflow.UpdatedAt);
}
