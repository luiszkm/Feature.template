using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Api.Tests.Ai.WorkflowHttp;

namespace Api.Tests.Ai;

public sealed class WorkflowRunnerTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task RunOnce_ShouldClaimRun_BeforeFirstLlmCall()
    {
        WebApplicationFactoryHolder holder = new();
        WorkflowRun? seen = null;
        var llm = new ScriptedLlmService(async (_, _) =>
        {
            seen ??= await LoadRunAsync(holder.Factory!, holder.RunId);
            return Reply("ok");
        });
        await using var factory = holder.Factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        holder.RunId = (await StartRunAsync(client, workflow.WorkflowId)).RunId;

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal(WorkflowRunStatus.Running, seen!.Status);
        Assert.NotNull(seen.StartedAt);
    }

    [Fact]
    public async Task RunOnce_ShouldStartNode_OnlyAfterAllPredecessorsSucceeded()
    {
        var starts = new ConcurrentDictionary<string, DateTime>();
        var ends = new ConcurrentDictionary<string, DateTime>();
        var llm = new ScriptedLlmService(async (request, ct) =>
        {
            var role = Role(request);
            starts[role] = DateTime.UtcNow;
            await Task.Delay(role is "b" ? 150 : role is "c" ? 50 : 10, ct);
            ends[role] = DateTime.UtcNow;
            return Reply(role);
        });
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agents = await RoleAgentsAsync(client, "a", "b", "c", "d");
        var workflow = await CreateAsync(client,
            [Node("a", agents["a"]), Node("b", agents["b"]), Node("c", agents["c"]), Node("d", agents["d"])],
            [Edge("a", "b"), Edge("a", "c"), Edge("b", "d"), Edge("c", "d")]);
        var run = await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        Assert.Equal("Succeeded", (await GetRunAsync(client, workflow.WorkflowId, run.RunId)).Status);
        Assert.True(starts["b"] >= ends["a"] && starts["c"] >= ends["a"]);
        Assert.True(starts["d"] >= ends["b"], "d started before b finished");
        Assert.True(starts["d"] >= ends["c"], "d started before c finished");
    }

    [Fact]
    public async Task RunOnce_ShouldRunReadyNodesConcurrently_UpToMaxParallelSteps()
    {
        var inFlight = 0;
        var max = 0;
        var llm = new ScriptedLlmService(async (_, ct) =>
        {
            var now = Interlocked.Increment(ref inFlight);
            int seen;
            while ((seen = Volatile.Read(ref max)) < now && Interlocked.CompareExchange(ref max, now, seen) != seen) { }
            await Task.Delay(200, ct);
            Interlocked.Decrement(ref inFlight);
            return Reply("ok");
        });
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client,
            [Node("a", agent.AgentId), Node("b", agent.AgentId), Node("c", agent.AgentId), Node("d", agent.AgentId)]);
        await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory, new WorkflowOptions { MaxParallelSteps = 3 }).RunOnceAsync(CancellationToken.None);

        Assert.Equal(4, llm.Requests.Count);
        Assert.Equal(3, max);
    }

    [Fact]
    public async Task Options_ShouldDefault_FromAppSettings()
    {
        await using var factory = TestWebApplicationFactory.Create();

        var options = factory.Services.GetRequiredService<IOptions<WorkflowOptions>>().Value;
        Assert.Equal(3, options.MaxParallelSteps);
        Assert.Equal(120, options.StepTimeoutSeconds);
        Assert.Equal(30, options.MaxRunMinutes);
        Assert.Equal(2, options.PollIntervalSeconds);

        var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(AppsettingsPath()));
        var block = json.RootElement.GetProperty("Ai").GetProperty("Workflows");
        Assert.Equal(3, block.GetProperty("MaxParallelSteps").GetInt32());
    }

    [Fact]
    public async Task RunOnce_ShouldComposeNodeMessage_InNodeOrder()
    {
        var prompts = new ConcurrentDictionary<string, string>();
        var llm = new ScriptedLlmService((request, _) =>
        {
            var role = Role(request);
            prompts[role] = request.UserPrompt;
            return Task.FromResult(Reply($"saida-{role}"));
        });
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agents = await RoleAgentsAsync(client, "a", "b", "c");
        // Edges listed b→c before a→c: the blocks still follow the node order a, b.
        var workflow = await CreateAsync(client,
            [Node("a", agents["a"], "Extraia"), Node("b", agents["b"]), Node("c", agents["c"], "Junte")],
            [Edge("b", "c"), Edge("a", "c")]);
        await StartRunAsync(client, workflow.WorkflowId, "hello");

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        Assert.Equal("Extraia\n\nhello", prompts["a"]);
        Assert.Equal("hello", prompts["b"]);
        Assert.Equal("Junte\n\nhello\n\n--- a ---\nsaida-a\n\n--- b ---\nsaida-b", prompts["c"]);
    }

    [Fact]
    public async Task RunOnce_ShouldPersistStep_BeforeStartingDependents()
    {
        WebApplicationFactoryHolder holder = new();
        WorkflowRunStep? aWhenBStarted = null;
        var llm = new ScriptedLlmService(async (request, _) =>
        {
            if (Role(request) == "b")
                aWhenBStarted = (await LoadRunAsync(holder.Factory!, holder.RunId)).Step("a");
            return new LlmResponse("saida", 5, InputTokens: 2, OutputTokens: 3, Cost: 0.004m);
        });
        await using var factory = holder.Factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agents = await RoleAgentsAsync(client, "a", "b");
        var workflow = await CreateAsync(client, [Node("a", agents["a"]), Node("b", agents["b"])], [Edge("a", "b")]);
        holder.RunId = (await StartRunAsync(client, workflow.WorkflowId)).RunId;

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        Assert.NotNull(aWhenBStarted);
        Assert.Equal(WorkflowStepStatus.Succeeded, aWhenBStarted!.Status);
        Assert.Equal("saida", aWhenBStarted.Output);
        Assert.Equal(2, aWhenBStarted.InputTokens);
        Assert.Equal(3, aWhenBStarted.OutputTokens);
        Assert.Equal(0.004m, aWhenBStarted.Cost);
        Assert.True(aWhenBStarted.LatencyMs >= 0);
        Assert.Equal(1, aWhenBStarted.IterationsUsed);
        Assert.NotNull(aWhenBStarted.StartedAt);
        Assert.NotNull(aWhenBStarted.FinishedAt);
    }

    [Fact]
    public async Task RunOnce_ShouldMarkRunSucceeded_WhenAllStepsSucceed()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var run = await LoadRunAsync(factory, created.RunId);
        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.All(run.Steps, s => Assert.Equal(WorkflowStepStatus.Succeeded, s.Status));
    }

    [Fact]
    public async Task RunOnce_ShouldFailStep_SkipDescendants_AndRunIndependentBranch()
    {
        var llm = new ScriptedLlmService((request, _) =>
            Role(request) == "a"
                ? throw new HttpRequestException("provider down")
                : Task.FromResult(Reply("ok")));
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agents = await RoleAgentsAsync(client, "a", "b", "c");
        var workflow = await CreateAsync(client,
            [Node("a", agents["a"]), Node("b", agents["b"]), Node("c", agents["c"])],
            [Edge("a", "b")]);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var run = await LoadRunAsync(factory, created.RunId);
        Assert.Equal(WorkflowStepStatus.Failed, run.Step("a").Status);
        Assert.Equal(nameof(HttpRequestException), run.Step("a").ErrorCode);
        Assert.Equal(WorkflowStepStatus.Skipped, run.Step("b").Status);
        Assert.Equal(WorkflowStepStatus.Succeeded, run.Step("c").Status);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.DoesNotContain(llm.Requests, r => Role(r) == "b");
    }

    [Fact]
    public async Task RunOnce_ShouldFailStepWithTimeout_WhenStepExceedsTimeout()
    {
        var llm = new ScriptedLlmService(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return Reply("late");
        });
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client, [Node("a", agent.AgentId)]);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory, new WorkflowOptions { StepTimeoutSeconds = 1 }).RunOnceAsync(CancellationToken.None);

        var run = await LoadRunAsync(factory, created.RunId);
        Assert.Equal(WorkflowStepStatus.Failed, run.Step("a").Status);
        Assert.Equal("Timeout", run.Step("a").ErrorCode);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task RunOnce_ShouldFailStepWithAgentUnavailable_WhenAgentInactive()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client, [Node("a", agent.AgentId)]);
        var created = await StartRunAsync(client, workflow.WorkflowId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/ai/agents/{agent.AgentId}")).StatusCode);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var run = await LoadRunAsync(factory, created.RunId);
        Assert.Equal(WorkflowStepStatus.Failed, run.Step("a").Status);
        Assert.Equal("AgentUnavailable", run.Step("a").ErrorCode);
        Assert.Empty(llm.Requests);
    }

    [Fact]
    public async Task RunOnce_ShouldFailStepWithQuotaExceeded_WhenQuotaExhausted()
    {
        var llm = ScriptedLlmService.Replying(input: 1, output: 1);
        await using var factory = Factory(llm, settings => settings["Ai:Quota:DailyTokensPerTenant"] = "2");
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client, [Node("a", agent.AgentId)]);
        var created = await StartRunAsync(client, workflow.WorkflowId);
        // Spend the whole quota after the run was accepted.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hi" })).StatusCode);
        var callsBefore = llm.Requests.Count;

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var run = await LoadRunAsync(factory, created.RunId);
        Assert.Equal(WorkflowStepStatus.Failed, run.Step("a").Status);
        Assert.Equal("QuotaExceeded", run.Step("a").ErrorCode);
        Assert.Equal(callsBefore, llm.Requests.Count);
    }

    [Theory]
    [InlineData("Admin", false)]
    [InlineData("ai.agent.manage", true)]
    public async Task RunOnce_ShouldRunToolsWithLauncherPrincipal(string grant, bool denied)
    {
        var toolOutputs = new ConcurrentQueue<string>();
        var llm = new ScriptedLlmService((request, _) =>
        {
            if (request.History is null)
                return Task.FromResult(new LlmResponse(string.Empty, 1, [new ToolCall("c1", "get_users_summary", [])]));
            foreach (var message in request.History.Where(m => m.Role == "tool"))
                toolOutputs.Enqueue(message.Content);
            return Task.FromResult(Reply("feito"));
        });
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client, tools: ["get_users_summary"]);
        var workflow = await CreateAsync(client, [Node("a", agent.AgentId)]);
        var principal = grant == "Admin"
            ? new RunPrincipal(Guid.NewGuid(), ["Admin"], [])
            : new RunPrincipal(Guid.NewGuid(), [], [grant]);
        await AddRunAsync(factory, workflow.WorkflowId, principal);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var output = Assert.Single(toolOutputs);
        Assert.Equal(denied, output.Contains("permission_denied"));
        if (!denied)
            Assert.Contains("total_count", output);
    }

    [Fact]
    public async Task RunOnce_ShouldRunStepInRunTenant()
    {
        var toolOutputs = new ConcurrentQueue<string>();
        var llm = new ScriptedLlmService((request, _) =>
        {
            if (request.History is null)
                return Task.FromResult(new LlmResponse(string.Empty, 1, [new ToolCall("c1", TenantProbeTool.Name, [])]));
            foreach (var message in request.History.Where(m => m.Role == "tool"))
                toolOutputs.Enqueue(message.Content);
            return Task.FromResult(Reply("feito"));
        });
        await using var factory = Factory(llm, services: s => s.AddScoped<ITool, TenantProbeTool>());
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client, tools: [TenantProbeTool.Name]);
        var workflow = await CreateAsync(client, [Node("a", agent.AgentId)]);
        await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        Assert.Contains($"tenant={TenantId}", Assert.Single(toolOutputs));
    }

    [Fact]
    public async Task RunOnce_ShouldTrackUsage_PerStep_WithWorkflowOperation()
    {
        var llm = new ScriptedLlmService((request, _) =>
            Role(request) == "b"
                ? throw new HttpRequestException("down")
                : Task.FromResult(new LlmResponse("ok", 7, InputTokens: 3, OutputTokens: 4, Cost: 0.005m)));
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agents = await RoleAgentsAsync(client, "a", "b");
        var workflow = await CreateAsync(client, [Node("a", agents["a"]), Node("b", agents["b"])]);
        await StartRunAsync(client, workflow.WorkflowId);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var entries = await InTenantAsync(factory, services =>
            services.GetRequiredService<AppDbContext>().Set<AiUsageEntry>()
                .Where(e => e.Operation == AiUsageOperations.Workflow)
                .ToListAsync());
        Assert.Equal(2, entries.Count);
        var ok = Assert.Single(entries, e => e.AgentId == agents["a"]);
        Assert.True(ok.Success);
        Assert.Equal(3, ok.InputTokens);
        Assert.Equal(4, ok.OutputTokens);
        Assert.Equal(0.005m, ok.Cost);
        Assert.False(string.IsNullOrEmpty(ok.Model));
        var failed = Assert.Single(entries, e => e.AgentId == agents["b"]);
        Assert.False(failed.Success);
        Assert.Equal(nameof(HttpRequestException), failed.ErrorCode);
    }

    [Fact]
    public async Task TryClaim_ShouldLetOnlyOneScopeWin()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        foreach (var scope in new[] { first, second })
            ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).SetTenant(TenantId);
        var firstRepo = first.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
        var secondRepo = second.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
        var firstRun = (await firstRepo.GetByIdAsync(created.RunId))!;
        var secondRun = (await secondRepo.GetByIdAsync(created.RunId))!;

        Assert.True(await firstRepo.TryClaimAsync(firstRun, DateTime.UtcNow));
        Assert.False(await secondRepo.TryClaimAsync(secondRun, DateTime.UtcNow));
    }

    [Fact]
    public async Task RunOnce_InParallel_ShouldExecuteEachNodeOnce()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        await Task.WhenAll(
            Runner(factory).RunOnceAsync(CancellationToken.None),
            Runner(factory).RunOnceAsync(CancellationToken.None));

        Assert.Equal(2, llm.Requests.Count);
        Assert.Equal(WorkflowRunStatus.Succeeded, (await LoadRunAsync(factory, created.RunId)).Status);
    }

    [Fact]
    public async Task RunOnce_ShouldInterruptRuns_OlderThanMaxRunMinutes()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client,
            [Node("a", agent.AgentId), Node("b", agent.AgentId), Node("c", agent.AgentId)],
            [Edge("a", "b")]);
        var stale = await AddRunAsync(factory, workflow.WorkflowId, new RunPrincipal(null, [], []), run =>
        {
            run.Claim(now.UtcDateTime.AddMinutes(-31));
            run.SucceedStep("a", new AgentResult("ok", 1, 2, 1, 1, null, []), 1, now.UtcDateTime.AddMinutes(-30));
            run.StartStep("b", now.UtcDateTime.AddMinutes(-30));
        });
        var fresh = await AddRunAsync(factory, workflow.WorkflowId, new RunPrincipal(null, [], []), run =>
            run.Claim(now.UtcDateTime.AddMinutes(-29)));

        await Runner(factory, new WorkflowOptions { MaxRunMinutes = 30 }, new FixedTimeProvider(now)).RunOnceAsync(CancellationToken.None);

        var interrupted = await LoadRunAsync(factory, stale);
        Assert.Equal(WorkflowRunStatus.Failed, interrupted.Status);
        Assert.Equal("Interrupted", interrupted.ErrorCode);
        Assert.Equal(WorkflowStepStatus.Succeeded, interrupted.Step("a").Status);
        Assert.Equal(WorkflowStepStatus.Skipped, interrupted.Step("b").Status);
        Assert.Equal(WorkflowStepStatus.Skipped, interrupted.Step("c").Status);
        Assert.Equal(WorkflowRunStatus.Running, (await LoadRunAsync(factory, fresh)).Status);
    }

    [Fact]
    public async Task RunOnce_ShouldExecuteGraphCopiedToRun()
    {
        var systemPrompts = new ConcurrentQueue<string>();
        var llm = new ScriptedLlmService((request, _) =>
        {
            systemPrompts.Enqueue(request.SystemPrompt ?? string.Empty);
            return Task.FromResult(Reply("ok"));
        });
        await using var factory = Factory(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agents = await RoleAgentsAsync(client, "antigo", "novo");
        var workflow = await CreateAsync(client, [Node("a", agents["antigo"])]);
        await StartRunAsync(client, workflow.WorkflowId);
        var put = await client.PutAsJsonAsync($"/api/v1/ai/workflows/{workflow.WorkflowId}", new
        {
            name = workflow.Name,
            nodes = new[] { Node("a", agents["novo"]) },
            edges = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var prompt = Assert.Single(systemPrompts);
        Assert.Contains("role:antigo", prompt);
    }

    [Fact]
    public async Task RunOnce_ShouldLogAndContinue_WhenRunProcessingThrows()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var broken = await StartRunAsync(client, workflow.WorkflowId);
        await Task.Delay(20);
        var healthy = await StartRunAsync(client, workflow.WorkflowId);
        await InTenantAsync(factory, async services =>
        {
            var db = services.GetRequiredService<AppDbContext>();
            var run = await db.Set<WorkflowRun>().SingleAsync(r => r.Id == broken.RunId);
            db.Entry(run).Property(r => r.GraphJson).CurrentValue = "{not json";
            return await db.SaveChangesAsync();
        });
        var logger = new ListLogger<WorkflowRunner>();

        await Runner(factory, logger: logger).RunOnceAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(broken.RunId.ToString()));
        Assert.Equal(WorkflowRunStatus.Succeeded, (await LoadRunAsync(factory, healthy.RunId)).Status);
    }

    [Fact]
    public async Task Execute_ShouldKeepPolling_AfterFailedPass()
    {
        var scopes = new ThrowingScopeFactory();
        var clock = new RecordingTimeProvider();
        var logger = new ListLogger<WorkflowRunner>();
        var runner = new WorkflowRunner(scopes, Options.Create(new WorkflowOptions { PollIntervalSeconds = 7 }), clock, logger);

        await runner.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (scopes.Calls < 3 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        await runner.StopAsync(CancellationToken.None);

        Assert.True(scopes.Calls >= 3, $"expected at least 3 passes, saw {scopes.Calls}");
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message == "Workflow runner pass failed");
        Assert.Equal(TimeSpan.FromSeconds(7), clock.LastDueTime);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static LlmResponse Reply(string text) => new(text, 2, InputTokens: 1, OutputTokens: 1, Cost: 0.001m);

    /// <summary>Agents whose instructions are <c>role:{name}</c>, so the LLM double can tell nodes apart.</summary>
    private static async Task<Dictionary<string, Guid>> RoleAgentsAsync(HttpClient client, params string[] roles)
    {
        var ids = new Dictionary<string, Guid>();
        foreach (var role in roles)
        {
            var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
            {
                name = $"Agent {role} {Guid.NewGuid():N}",
                instructions = $"role:{role}",
                toolNames = Array.Empty<string>()
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids[role] = (await response.Content.ReadFromJsonAsync<AgentOutput>())!.AgentId;
        }
        return ids;
    }

    private static string Role(LlmRequest request)
    {
        var prompt = request.SystemPrompt ?? string.Empty;
        var start = prompt.IndexOf("role:", StringComparison.Ordinal);
        return start < 0 ? string.Empty : new string(prompt[(start + 5)..].TakeWhile(char.IsLetterOrDigit).ToArray());
    }

    private static Task<Guid> AddRunAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        Guid workflowId,
        RunPrincipal principal,
        Action<WorkflowRun>? arrange = null) =>
        InTenantAsync(factory, async services =>
        {
            var workflow = (await services.GetRequiredService<IWorkflowRepository>().GetByIdAsync(workflowId))!;
            var run = WorkflowRun.Create(TenantId, workflow, "hello", principal);
            arrange?.Invoke(run);
            await services.GetRequiredService<IWorkflowRunRepository>().AddAsync(run);
            await services.GetRequiredService<AppDbContext>().SaveChangesAsync();
            return run.Id;
        });

    private static string AppsettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "features.json")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "Api", "appsettings.json");
    }

    /// <summary>Lets an LLM double read state the test only knows after the factory exists.</summary>
    private sealed class WebApplicationFactoryHolder
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? Factory { get; set; }
        public Guid RunId { get; set; }
    }

    private sealed class TenantProbeTool(ITenantContext tenant) : ITool
    {
        public const string Name = "probe_tenant";

        public ToolDefinition Definition { get; } = new(Name, "Returns the tenant of the scope.", new JsonObject { ["type"] = "object" });

        public Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default) =>
            Task.FromResult($"tenant={tenant.TenantId}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Every timer fires at once and the due time is recorded.</summary>
    private sealed class RecordingTimeProvider : TimeProvider
    {
        public TimeSpan LastDueTime { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            LastDueTime = dueTime;
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _calls);
            throw new InvalidOperationException("database unavailable");
        }
    }
}
