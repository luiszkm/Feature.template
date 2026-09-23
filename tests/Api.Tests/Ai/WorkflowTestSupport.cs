using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

internal static class WorkflowHttp
{
    /// <summary>
    /// A host with its own InMemory store. The worker polls every tenant's queue, so sharing the
    /// process-wide `AppDb` store would let another test's host execute this test's runs with its
    /// own LLM double. The hosted worker sleeps an hour unless <paramref name="pollSeconds"/> says otherwise.
    /// </summary>
    public static WebApplicationFactory<Program> Factory(
        ILlmService? llm = null,
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null,
        int pollSeconds = 3600)
    {
        var databaseName = $"workflows-{Guid.NewGuid():N}";
        return AiHttp.Factory(settings =>
            {
                settings["Ai:Workflows:PollIntervalSeconds"] = pollSeconds.ToString();
                configure?.Invoke(settings);
            })
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(collection =>
            {
                collection.AddSingleton(llm ?? ScriptedLlmService.Replying());
                collection.ConfigureDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services?.Invoke(collection);
            }));
    }

    public static object Node(string key, Guid agentId, string? instruction = null, double x = 0, double y = 0) =>
        new { key, agentId, instruction, x, y };

    public static object Edge(string from, string to) => new { from, to };

    public static async Task<WorkflowOutput> CreateAsync(HttpClient client, object[] nodes, object[]? edges = null, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/ai/workflows", new
        {
            name = name ?? $"Workflow {Guid.NewGuid():N}",
            description = "d",
            nodes,
            edges = edges ?? []
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkflowOutput>())!;
    }

    /// <summary>Two agents and a workflow A → B.</summary>
    public static async Task<(WorkflowOutput Workflow, AgentOutput A, AgentOutput B)> CreateChainAsync(HttpClient client)
    {
        var a = await AiHttp.CreateAgentAsync(client);
        var b = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client, [Node("a", a.AgentId), Node("b", b.AgentId)], [Edge("a", "b")]);
        return (workflow, a, b);
    }

    public static async Task<WorkflowRunOutput> StartRunAsync(HttpClient client, Guid workflowId, string input = "hello")
    {
        var response = await client.PostAsJsonAsync($"/api/v1/ai/workflows/{workflowId}/runs", new { input });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkflowRunOutput>())!;
    }

    public static async Task<WorkflowRunOutput> GetRunAsync(HttpClient client, Guid workflowId, Guid runId) =>
        (await client.GetFromJsonAsync<WorkflowRunOutput>($"/api/v1/ai/workflows/{workflowId}/runs/{runId}"))!;

    public static WorkflowRunner Runner(
        WebApplicationFactory<Program> factory,
        WorkflowOptions? options = null,
        TimeProvider? clock = null,
        ILogger<WorkflowRunner>? logger = null) =>
        new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options ?? new WorkflowOptions()),
            clock ?? TimeProvider.System,
            logger ?? new ListLogger<WorkflowRunner>());

    /// <summary>Runs <paramref name="action"/> in a scope bound to the `dev` tenant.</summary>
    public static async Task<T> InTenantAsync<T>(WebApplicationFactory<Program> factory, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).SetTenant(TenantTestDefaults.DevelopmentTenantId);
        return await action(scope.ServiceProvider);
    }

    public static Task<WorkflowRun> LoadRunAsync(WebApplicationFactory<Program> factory, Guid runId) =>
        InTenantAsync(factory, async services =>
            (await services.GetRequiredService<IWorkflowRunRepository>().GetByIdAsync(runId))!);
}
