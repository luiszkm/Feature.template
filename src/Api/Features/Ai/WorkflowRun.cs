using System.Security.Claims;
using System.Text.Json;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

public enum WorkflowRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}

public enum WorkflowStepStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped
}

/// <summary>
/// One execution of a workflow. It keeps its own copy of the graph and of the launcher's principal,
/// so an edit to the workflow or a request that ended does not change what the worker runs.
/// </summary>
public sealed class WorkflowRun : AggregateRoot, IMultiTenantEntity
{
    public const string InterruptedErrorCode = "Interrupted";
    public const string InternalErrorCode = "InternalError";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly List<WorkflowRunStep> _steps = [];

    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public string Input { get; private set; } = string.Empty;
    public WorkflowRunStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string GraphJson { get; private set; } = string.Empty;
    public string PrincipalJson { get; private set; } = string.Empty;
    public Guid Version { get; private set; }
    public IReadOnlyList<WorkflowRunStep> Steps => _steps;

    private WorkflowRun() { }

    public static WorkflowRun Create(Guid tenantId, Workflow workflow, string input, RunPrincipal principal)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        var graph = new RunGraph(
            workflow.Nodes.Select(n => new RunGraphNode(n.Key, n.AgentId, n.Instruction, n.X, n.Y)).ToList(),
            workflow.Edges.Select(e => new RunGraphEdge(e.FromKey, e.ToKey)).ToList());

        var run = new WorkflowRun
        {
            TenantId = tenantId,
            WorkflowId = workflow.Id,
            Input = input,
            Status = WorkflowRunStatus.Queued,
            CreatedByUserId = principal.UserId,
            GraphJson = JsonSerializer.Serialize(graph, Json),
            PrincipalJson = JsonSerializer.Serialize(principal, Json),
            Version = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        run._steps.AddRange(graph.Nodes.Select(n => new WorkflowRunStep(n.Key, n.AgentId)));
        return run;
    }

    public RunGraph Graph => JsonSerializer.Deserialize<RunGraph>(GraphJson, Json)
        ?? throw new InvalidOperationException($"Workflow run '{Id}' has no graph.");

    public RunPrincipal Principal => JsonSerializer.Deserialize<RunPrincipal>(PrincipalJson, Json)
        ?? throw new InvalidOperationException($"Workflow run '{Id}' has no principal.");

    public decimal? TotalCost =>
        _steps.Any(s => s.Cost is not null) ? _steps.Sum(s => s.Cost ?? 0m) : null;

    public WorkflowRunStep Step(string nodeKey) => _steps.Single(s => s.NodeKey == nodeKey);

    /// <summary>Queued → Running; false when another worker already took it.</summary>
    public bool Claim(DateTime now)
    {
        if (Status != WorkflowRunStatus.Queued)
            return false;

        Status = WorkflowRunStatus.Running;
        StartedAt = now;
        Touch();
        return true;
    }

    public void StartStep(string nodeKey, DateTime now)
    {
        Step(nodeKey).Start(now);
        Touch();
    }

    public void SucceedStep(string nodeKey, AgentResult result, long latencyMs, DateTime now)
    {
        Step(nodeKey).Succeed(result, latencyMs, now);
        Touch();
    }

    public void FailStep(string nodeKey, string errorCode, long latencyMs, DateTime now)
    {
        Step(nodeKey).Fail(errorCode, latencyMs, now);
        Touch();
    }

    public void SkipStep(string nodeKey)
    {
        Step(nodeKey).Skip();
        Touch();
    }

    public void Finish(DateTime now)
    {
        Status = _steps.All(s => s.Status == WorkflowStepStatus.Succeeded)
            ? WorkflowRunStatus.Succeeded
            : WorkflowRunStatus.Failed;
        FinishedAt = now;
        Touch();
    }

    /// <summary>Ends a run nobody is going to finish; what already finished stays as it was.</summary>
    public void Abort(string errorCode, DateTime now)
    {
        foreach (var step in _steps.Where(s => s.Status is WorkflowStepStatus.Pending or WorkflowStepStatus.Running))
            step.Skip();

        Status = WorkflowRunStatus.Failed;
        ErrorCode = errorCode;
        FinishedAt = now;
        Touch();
    }

    private void Touch() => Version = Guid.NewGuid();
}

public sealed class WorkflowRunStep
{
    public string NodeKey { get; private set; } = string.Empty;
    public Guid AgentId { get; private set; }
    public WorkflowStepStatus Status { get; private set; }
    public string? Output { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal? Cost { get; private set; }
    public long LatencyMs { get; private set; }
    public int IterationsUsed { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    private WorkflowRunStep() { }

    public WorkflowRunStep(string nodeKey, Guid agentId)
    {
        NodeKey = nodeKey;
        AgentId = agentId;
        Status = WorkflowStepStatus.Pending;
    }

    internal void Start(DateTime now)
    {
        Status = WorkflowStepStatus.Running;
        StartedAt = now;
    }

    internal void Succeed(AgentResult result, long latencyMs, DateTime now)
    {
        Status = WorkflowStepStatus.Succeeded;
        Output = result.Reply;
        InputTokens = result.InputTokens;
        OutputTokens = result.OutputTokens;
        Cost = result.Cost;
        IterationsUsed = result.IterationsUsed;
        LatencyMs = latencyMs;
        FinishedAt = now;
    }

    internal void Fail(string errorCode, long latencyMs, DateTime now)
    {
        Status = WorkflowStepStatus.Failed;
        ErrorCode = errorCode;
        LatencyMs = latencyMs;
        FinishedAt = now;
    }

    internal void Skip() => Status = WorkflowStepStatus.Skipped;
}

public sealed record RunGraph(IReadOnlyList<RunGraphNode> Nodes, IReadOnlyList<RunGraphEdge> Edges);

public sealed record RunGraphNode(string Key, Guid AgentId, string? Instruction, double X, double Y);

public sealed record RunGraphEdge(string From, string To);

/// <summary>Who launched the run, as the worker has to present it to the tools.</summary>
public sealed record RunPrincipal(Guid? UserId, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public const string AuthenticationType = "workflow-run";

    public static RunPrincipal From(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return new RunPrincipal(
            Guid.TryParse(id, out var userId) ? userId : null,
            user.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList(),
            user.FindAll(AuthorizationClaimTypes.Permission).Select(c => c.Value).Distinct().ToList());
    }

    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>();
        if (UserId is { } userId)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        claims.AddRange(Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(Permissions.Select(permission => new Claim(AuthorizationClaimTypes.Permission, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
    }
}

public interface IWorkflowRunRepository
{
    Task<WorkflowRun?> GetByIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<WorkflowRun>> ListAsync(Guid workflowId, ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowRun run, CancellationToken cancellationToken = default);

    /// <summary>Claims a loaded run and saves it; false when it was no longer queued or another scope saved first.</summary>
    Task<bool> TryClaimAsync(WorkflowRun run, DateTime now, CancellationToken cancellationToken = default);
}

internal sealed class WorkflowRunRepository(AppDbContext db) : IWorkflowRunRepository
{
    public Task<WorkflowRun?> GetByIdAsync(Guid runId, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<WorkflowRun>(), runId, cancellationToken);

    public Task<PaginatedListOutput<WorkflowRun>> ListAsync(
        Guid workflowId,
        ListQuery listQuery,
        CancellationToken cancellationToken = default) =>
        db.Set<WorkflowRun>()
            .AsNoTracking()
            .Where(r => r.WorkflowId == workflowId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToPaginatedListAsync(listQuery, cancellationToken);

    public Task AddAsync(WorkflowRun run, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<WorkflowRun>(), run, cancellationToken);

    public async Task<bool> TryClaimAsync(WorkflowRun run, DateTime now, CancellationToken cancellationToken = default)
    {
        if (!run.Claim(now))
            return false;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(run).State = EntityState.Detached;
            return false;
        }
    }
}

internal sealed class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> entity)
    {
        entity.ToTable("AiWorkflowRuns");
        entity.HasKey(r => r.Id);
        entity.Property(r => r.Input).HasMaxLength(4000).IsRequired();
        entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(r => r.ErrorCode).HasMaxLength(200);
        entity.Property(r => r.GraphJson).IsRequired();
        entity.Property(r => r.PrincipalJson).IsRequired();
        entity.Property(r => r.Version).IsConcurrencyToken();
        entity.HasOne<Workflow>()
            .WithMany()
            .HasForeignKey(r => r.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(r => new { r.TenantId, r.WorkflowId, r.CreatedAt });
        entity.HasIndex(r => r.Status);
        entity.Ignore(r => r.Graph);
        entity.Ignore(r => r.Principal);
        entity.Ignore(r => r.TotalCost);

        entity.OwnsMany(r => r.Steps, step =>
        {
            step.ToTable("AiWorkflowRunSteps");
            step.WithOwner().HasForeignKey("WorkflowRunId");
            step.Property<int>("Id");
            step.HasKey("Id");
            step.Property(s => s.NodeKey).HasMaxLength(50).IsRequired();
            step.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            step.Property(s => s.Cost).HasPrecision(18, 8);
            step.Property(s => s.ErrorCode).HasMaxLength(200);
            step.HasIndex("WorkflowRunId", nameof(WorkflowRunStep.NodeKey)).IsUnique();
        });
        entity.Navigation(r => r.Steps)
            .HasField("_steps")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
