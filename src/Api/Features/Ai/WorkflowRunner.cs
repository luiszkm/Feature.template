using System.Diagnostics;
using System.Text;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed class WorkflowOptions
{
    public const string SectionName = "Ai:Workflows";

    public int MaxParallelSteps { get; set; } = 3;
    public int StepTimeoutSeconds { get; set; } = 120;
    public int MaxRunMinutes { get; set; } = 30;
    public int PollIntervalSeconds { get; set; } = 2;
}

/// <summary>
/// Executes queued workflow runs. Polls the database rather than an in-memory queue, so a run
/// accepted with <c>202</c> survives a restart and any instance can take it; the claim is guarded
/// by the run's concurrency token. Runs outside any request, across every tenant.
/// </summary>
internal sealed class WorkflowRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkflowOptions> options,
    TimeProvider clock,
    ILogger<WorkflowRunner> logger) : BackgroundService
{
    public const string TimeoutErrorCode = "Timeout";
    public const string AgentUnavailableErrorCode = "AgentUnavailable";
    public const string QuotaExceededErrorCode = "QuotaExceeded";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>One pass: interrupt what is stale, then run what is queued. Never throws.</summary>
    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<QueuedRun> queued;
        try
        {
            await InterruptStaleAsync(cancellationToken);
            queued = await QueuedAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow runner pass failed");
            return;
        }

        foreach (var run in queued)
        {
            try
            {
                await ProcessAsync(run, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow run {RunId} failed outside a step", run.Id);
            }
        }
    }

    private async Task InterruptStaleAsync(CancellationToken cancellationToken)
    {
        var now = Now();
        var cutoff = now.AddMinutes(-options.Value.MaxRunMinutes);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await db.Set<WorkflowRun>()
            .IgnoreQueryFilters()
            .Where(r => r.Status == WorkflowRunStatus.Running && r.StartedAt < cutoff)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return;

        foreach (var run in stale)
            run.Abort(WorkflowRun.InterruptedErrorCode, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Workflow runner interrupted {Count} runs started before {Cutoff}", stale.Count, cutoff);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another instance finished or interrupted them first; the next pass sees the result.
        }
    }

    private async Task<IReadOnlyList<QueuedRun>> QueuedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<WorkflowRun>()
            .IgnoreQueryFilters()
            .Where(r => r.Status == WorkflowRunStatus.Queued)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new QueuedRun(r.Id, r.TenantId))
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessAsync(QueuedRun queued, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        SetTenant(services, queued.TenantId);
        var runs = services.GetRequiredService<IWorkflowRunRepository>();
        var db = services.GetRequiredService<AppDbContext>();

        var run = await runs.GetByIdAsync(queued.Id, cancellationToken);
        if (run is null || !await runs.TryClaimAsync(run, Now(), cancellationToken))
            return;

        logger.LogInformation("Workflow run {RunId} started for tenant {TenantId}", run.Id, run.TenantId);
        try
        {
            await ExecuteRunAsync(run, db, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await TryAbortAsync(run, db);
            throw;
        }

        logger.LogInformation("Workflow run {RunId} finished {Status}", run.Id, run.Status);
    }

    private async Task ExecuteRunAsync(WorkflowRun run, AppDbContext db, CancellationToken cancellationToken)
    {
        var graph = run.Graph;
        var principal = run.Principal;
        var keys = graph.Nodes.Select(n => n.Key).ToList();
        var nodes = graph.Nodes.ToDictionary(n => n.Key, StringComparer.Ordinal);
        var predecessors = WorkflowGraph.Predecessors(keys, graph.Edges.Select(e => (e.From, e.To)).ToList());
        var running = new Dictionary<Task<StepOutcome>, string>();
        var maxParallel = Math.Max(1, options.Value.MaxParallelSteps);

        while (true)
        {
            SkipUnreachable(run, keys, predecessors);

            var ready = keys.Where(key =>
                run.Step(key).Status == WorkflowStepStatus.Pending &&
                predecessors[key].All(p => run.Step(p).Status == WorkflowStepStatus.Succeeded));

            foreach (var key in ready.Take(maxParallel - running.Count).ToList())
            {
                var message = ComposeMessage(run.Input, nodes[key].Instruction, predecessors[key].Select(p => (p, run.Step(p).Output)));
                run.StartStep(key, Now());
                await db.SaveChangesAsync(cancellationToken);
                running[ExecuteStepAsync(run.TenantId, principal, nodes[key].AgentId, message, cancellationToken)] = key;
            }

            if (running.Count == 0)
                break;

            var done = await Task.WhenAny(running.Keys);
            var nodeKey = running[done];
            running.Remove(done);
            var outcome = await done;

            if (outcome.Result is { } result)
                run.SucceedStep(nodeKey, result, outcome.LatencyMs, Now());
            else
                run.FailStep(nodeKey, outcome.ErrorCode!, outcome.LatencyMs, Now());
            await db.SaveChangesAsync(cancellationToken);
        }

        run.Finish(Now());
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A node whose predecessor failed or was skipped will never be ready.</summary>
    private static void SkipUnreachable(
        WorkflowRun run,
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, IReadOnlyList<string>> predecessors)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var key in keys)
            {
                if (run.Step(key).Status != WorkflowStepStatus.Pending)
                    continue;
                if (predecessors[key].Any(p => run.Step(p).Status is WorkflowStepStatus.Failed or WorkflowStepStatus.Skipped))
                {
                    run.SkipStep(key);
                    changed = true;
                }
            }
        } while (changed);
    }

    internal static string ComposeMessage(string input, string? instruction, IEnumerable<(string Key, string? Output)> predecessors)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(instruction))
            builder.Append(instruction).Append("\n\n");
        builder.Append(input);
        foreach (var (key, output) in predecessors)
            builder.Append("\n\n--- ").Append(key).Append(" ---\n").Append(output);
        return builder.ToString();
    }

    /// <summary>One DI scope per step: tools use AppDbContext, which does not allow concurrent use.</summary>
    private async Task<StepOutcome> ExecuteStepAsync(
        Guid tenantId,
        RunPrincipal principal,
        Guid agentId,
        string message,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        SetTenant(services, tenantId);
        services.GetRequiredService<BackgroundPrincipal>().Set(principal.ToClaimsPrincipal());
        services.GetRequiredService<IAgentRuntimeContext>().Set(agentId);

        var agent = await services.GetRequiredService<IAgentRepository>().GetByIdAsync(agentId, cancellationToken);
        if (agent is null || !agent.IsActive)
            return StepOutcome.Failed(AgentUnavailableErrorCode, 0);

        try
        {
            await services.GetRequiredService<AiQuota>().EnsureWithinAsync(cancellationToken);
        }
        catch (TooManyRequestsException)
        {
            return StepOutcome.Failed(QuotaExceededErrorCode, 0);
        }

        var llmOptions = services.GetRequiredService<IOptions<LlmOptions>>().Value;
        var environment = services.GetRequiredService<IHostEnvironment>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.StepTimeoutSeconds)));
        var stopwatch = Stopwatch.StartNew();

        StepOutcome outcome;
        try
        {
            var result = await services.GetRequiredService<AgentLoop>().RunAsync(
                message,
                agent.Instructions,
                history: null,
                agent.ToolNames,
                timeout.Token,
                agent.Model);
            outcome = StepOutcome.Succeeded(result, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Workflow step for agent {AgentId} timed out", agentId);
            outcome = StepOutcome.Failed(TimeoutErrorCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Workflow step for agent {AgentId} failed", agentId);
            outcome = StepOutcome.Failed(ex.GetType().Name, stopwatch.ElapsedMilliseconds);
        }

        await services.GetRequiredService<IAiUsageTracker>().TrackAsync(
            new AiUsageRecord(
                Service: "llm",
                Provider: LlmServiceResolver.ProviderLabel(environment, llmOptions),
                Model: agent.Model ?? llmOptions.Model,
                Module: "ai",
                Operation: AiUsageOperations.Workflow,
                TenantId: tenantId,
                AgentId: agentId,
                InputTokens: outcome.Result?.InputTokens ?? 0,
                OutputTokens: outcome.Result?.OutputTokens ?? 0,
                Cost: outcome.Result?.Cost,
                Latency: TimeSpan.FromMilliseconds(outcome.LatencyMs),
                Success: outcome.Result is not null,
                ErrorCode: outcome.ErrorCode),
            CancellationToken.None);

        return outcome;
    }

    private async Task TryAbortAsync(WorkflowRun run, AppDbContext db)
    {
        try
        {
            run.Abort(WorkflowRun.InternalErrorCode, Now());
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow run {RunId} could not be marked failed", run.Id);
        }
    }

    private static void SetTenant(IServiceProvider services, Guid tenantId)
    {
        if (services.GetRequiredService<ITenantContext>() is TenantContext tenant)
            tenant.SetTenant(tenantId);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private sealed record QueuedRun(Guid Id, Guid TenantId);

    private sealed record StepOutcome(AgentResult? Result, string? ErrorCode, long LatencyMs)
    {
        public static StepOutcome Succeeded(AgentResult result, long latencyMs) => new(result, null, latencyMs);

        public static StepOutcome Failed(string errorCode, long latencyMs) => new(null, errorCode, latencyMs);
    }
}
