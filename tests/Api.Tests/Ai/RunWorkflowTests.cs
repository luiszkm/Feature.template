using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using static Api.Tests.Ai.WorkflowHttp;

namespace Api.Tests.Ai;

public sealed class RunWorkflowTests
{
    private static string Runs(Guid workflowId) => $"/api/v1/ai/workflows/{workflowId}/runs";

    // ---- accept ----------------------------------------------------------------------------

    [Fact]
    public async Task PostRun_ShouldReturn202_BeforeAnyLlmCallCompletes()
    {
        var release = new TaskCompletionSource();
        var llm = new ScriptedLlmService(async (_, _) =>
        {
            await release.Task;
            return new LlmResponse("ok", 2, InputTokens: 1, OutputTokens: 1);
        });
        // The worker polls every second, so it may pick the run up - and then block on the LLM.
        await using var factory = Factory(llm, pollSeconds: 1);
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);

        try
        {
            var response = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input = "hello" });

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var run = (await response.Content.ReadFromJsonAsync<WorkflowRunOutput>())!;
            Assert.Equal($"{Runs(workflow.WorkflowId)}/{run.RunId}", response.Headers.Location?.OriginalString);
            Assert.Equal("Queued", run.Status);
            Assert.Equal(["a", "b"], run.Steps.Select(s => s.NodeKey));
            Assert.All(run.Steps, s => Assert.Equal("Pending", s.Status));
            Assert.All(run.Steps, s => Assert.Null(s.Output));
        }
        finally
        {
            release.SetResult();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("4001")]
    public async Task PostRun_ShouldReturn400_ForInvalidInput(string caseName)
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var input = caseName == "" ? "" : new string('x', 4001);

        var response = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.True(problem.Errors.ContainsKey("input"));
    }

    [Fact]
    public async Task PostRun_ShouldReturn400_AndPersistNothing_WhenGuardBlocks()
    {
        await using var factory = Factory(services: s => s.AddSingleton<IContentGuard>(new BlockingContentGuard(GuardSubject.UserMessage)));
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);

        var response = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input = "hello" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var page = (await client.GetFromJsonAsync<PaginatedListOutput<WorkflowRunSummaryOutput>>(Runs(workflow.WorkflowId)))!;
        Assert.Empty(page.Data);
    }

    [Fact]
    public async Task PostRun_ShouldReturn429_AndPersistNothing_WhenQuotaExhausted()
    {
        await using var factory = Factory(
            ScriptedLlmService.Replying(input: 1, output: 1),
            settings => settings["Ai:Quota:DailyTokensPerTenant"] = "2");
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        // One chat spends the whole daily quota of 2 tokens.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hi" })).StatusCode);

        var response = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input = "hello" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var page = (await client.GetFromJsonAsync<PaginatedListOutput<WorkflowRunSummaryOutput>>(Runs(workflow.WorkflowId)))!;
        Assert.Empty(page.Data);
    }

    [Fact]
    public async Task PostRun_ShouldReturn429_WhenAiRateLimitExceeded()
    {
        await using var factory = Factory(configure: settings => settings["Ai:RateLimit:PermitLimit"] = "1");
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);

        var first = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input = "hello" });
        var second = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input = "hello" });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task PostRun_ShouldSnapshotLauncherPrincipal()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var token = TokenClaims(client);

        var created = await StartRunAsync(client, workflow.WorkflowId);
        var principal = (await LoadRunAsync(factory, created.RunId)).Principal;

        Assert.Equal(Guid.Parse(token.GetProperty("sub").GetString()!), principal.UserId);
        Assert.Equal(principal.UserId, created.CreatedByUserId);
        Assert.Contains("Admin", principal.Roles);
        Assert.Equal(Values(token, "permission").Order(), principal.Permissions.Order());
    }

    [Fact]
    public async Task PostRun_ShouldCopyGraph_SoLaterPutDoesNotChangeRun()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, a, b) = await CreateChainAsync(client);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        var put = await client.PutAsJsonAsync($"/api/v1/ai/workflows/{workflow.WorkflowId}", new
        {
            name = "outro",
            nodes = new[] { Node("x", a.AgentId), Node("y", b.AgentId), Node("z", b.AgentId) },
            edges = new[] { Edge("x", "z") }
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var run = await GetRunAsync(client, workflow.WorkflowId, created.RunId);
        Assert.Equal(["a", "b"], run.Nodes.Select(n => n.Key));
        Assert.Equal([new WorkflowEdgeOutput("a", "b")], run.Edges);
    }

    [Fact]
    public async Task PostRun_ShouldReturn404_WhenWorkflowDeactivated()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/ai/workflows/{workflow.WorkflowId}")).StatusCode);

        var response = await client.PostAsJsonAsync(Runs(workflow.WorkflowId), new { input = "hello" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Run_ShouldReachSucceeded_ThroughHostedWorker()
    {
        await using var factory = Factory(ScriptedLlmService.Replying("feito"), pollSeconds: 1);
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var created = await StartRunAsync(client, workflow.WorkflowId);

        var run = created;
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (run.Status is "Queued" or "Running" && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            run = await GetRunAsync(client, workflow.WorkflowId, created.RunId);
        }

        Assert.Equal("Succeeded", run.Status);
        Assert.All(run.Steps, s => Assert.Equal("feito", s.Output));
    }

    // ---- read ------------------------------------------------------------------------------

    [Fact]
    public async Task GetRun_ShouldReturnFullShape()
    {
        await using var factory = Factory(ScriptedLlmService.Replying("resposta", input: 3, output: 4, cost: 0.001m));
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, a, b) = await CreateChainAsync(client);
        var created = await StartRunAsync(client, workflow.WorkflowId, "o input");
        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var run = await GetRunAsync(client, workflow.WorkflowId, created.RunId);

        Assert.Equal(created.RunId, run.RunId);
        Assert.Equal(workflow.WorkflowId, run.WorkflowId);
        Assert.Equal("Succeeded", run.Status);
        Assert.Equal("o input", run.Input);
        Assert.Null(run.ErrorCode);
        Assert.NotEqual(default, run.CreatedAt);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.FinishedAt);
        Assert.NotNull(run.CreatedByUserId);
        Assert.Equal(0.002m, run.TotalCost);
        Assert.Equal(["a", "b"], run.Nodes.Select(n => n.Key));
        Assert.Equal([new WorkflowEdgeOutput("a", "b")], run.Edges);
        Assert.Equal([a.AgentId, b.AgentId], run.Steps.Select(s => s.AgentId));
        Assert.All(run.Steps, step =>
        {
            Assert.Equal("Succeeded", step.Status);
            Assert.Equal("resposta", step.Output);
            Assert.Equal(3, step.InputTokens);
            Assert.Equal(4, step.OutputTokens);
            Assert.Equal(0.001m, step.Cost);
            Assert.True(step.LatencyMs >= 0);
            Assert.Equal(1, step.IterationsUsed);
            Assert.Null(step.ErrorCode);
            Assert.NotNull(step.StartedAt);
            Assert.NotNull(step.FinishedAt);
        });
    }

    [Fact]
    public void TotalCost_ShouldSumStepCosts_OrBeNull()
    {
        var workflow = Workflow.Create(
            Guid.NewGuid(), "w", null,
            [new WorkflowNode("a", Guid.NewGuid(), null, 0, 0), new WorkflowNode("b", Guid.NewGuid(), null, 0, 0)], []);

        var priced = WorkflowRun.Create(workflow.TenantId, workflow, "i", new RunPrincipal(null, [], []));
        priced.SucceedStep("a", new AgentResult("x", 1, 2, 1, 1, 0.001m, []), 5, DateTime.UtcNow);
        priced.SucceedStep("b", new AgentResult("y", 1, 2, 1, 1, 0.002m, []), 5, DateTime.UtcNow);
        Assert.Equal(0.003m, priced.TotalCost);

        var unpriced = WorkflowRun.Create(workflow.TenantId, workflow, "i", new RunPrincipal(null, [], []));
        unpriced.SucceedStep("a", new AgentResult("x", 1, 2, 1, 1, null, []), 5, DateTime.UtcNow);
        Assert.Null(unpriced.TotalCost);
    }

    [Fact]
    public async Task ListRuns_ShouldReturnPreviews_NewestFirst()
    {
        await using var factory = Factory(ScriptedLlmService.Replying(cost: 0.001m));
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var older = await StartRunAsync(client, workflow.WorkflowId, new string('x', 300));
        await Task.Delay(20);
        var newer = await StartRunAsync(client, workflow.WorkflowId, "curto");
        await Runner(factory).RunOnceAsync(CancellationToken.None);

        var page = (await client.GetFromJsonAsync<PaginatedListOutput<WorkflowRunSummaryOutput>>(Runs(workflow.WorkflowId)))!;

        Assert.Equal([newer.RunId, older.RunId], page.Data.Select(r => r.RunId));
        Assert.Equal("curto", page.Data[0].InputPreview);
        Assert.Equal(new string('x', 200), page.Data[1].InputPreview);
        Assert.All(page.Data, r =>
        {
            Assert.Equal("Succeeded", r.Status);
            Assert.Equal(0.002m, r.TotalCost);
            Assert.NotNull(r.FinishedAt);
            Assert.NotEqual(default, r.CreatedAt);
        });
    }

    [Fact]
    public async Task GetRun_ShouldReturn404_ForRunOfAnotherWorkflow()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (first, _, _) = await CreateChainAsync(client);
        var (second, _, _) = await CreateChainAsync(client);
        var run = await StartRunAsync(client, first.WorkflowId);

        var response = await client.GetAsync($"{Runs(second.WorkflowId)}/{run.RunId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Repository_ShouldNotFindRun_OfAnotherTenant()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);
        var run = await StartRunAsync(client, workflow.WorkflowId);

        await using var scope = factory.Services.CreateAsyncScope();
        ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).SetTenant(Guid.NewGuid());

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>().GetByIdAsync(run.RunId));
    }

    [Fact]
    public void Statuses_ShouldExposeOnlyDeclaredValues()
    {
        Assert.Equal(["Queued", "Running", "Succeeded", "Failed"], Enum.GetNames<WorkflowRunStatus>());
        Assert.Equal(["Pending", "Running", "Succeeded", "Failed", "Skipped"], Enum.GetNames<WorkflowStepStatus>());
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static JsonElement TokenClaims(HttpClient client)
    {
        var payload = client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload))).RootElement;
    }

    private static IReadOnlyList<string> Values(JsonElement claims, string name) =>
        !claims.TryGetProperty(name, out var value) ? []
        : value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(v => v.GetString()!).ToList()
        : [value.GetString()!];
}
