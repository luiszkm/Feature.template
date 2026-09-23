using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Api.Tests.Ai.WorkflowHttp;

namespace Api.Tests.Ai;

public sealed class CreateWorkflowTests
{
    private const string Base = "/api/v1/ai/workflows";

    // ---- create ----------------------------------------------------------------------------

    [Fact]
    public async Task Post_ShouldReturn201_WithLocationAndBody()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var a = await AiHttp.CreateAgentAsync(client);
        var b = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync(Base, new
        {
            name = "Triagem",
            description = "Classifica e responde",
            nodes = new[] { Node("a", a.AgentId, "Extraia os factos", 10, 20), Node("b", b.AgentId, null, 300, 20) },
            edges = new[] { Edge("a", "b") }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var output = (await response.Content.ReadFromJsonAsync<WorkflowOutput>())!;
        Assert.Equal($"{Base}/{output.WorkflowId}", response.Headers.Location?.OriginalString);
        Assert.NotEqual(Guid.Empty, output.WorkflowId);
        Assert.Equal("Triagem", output.Name);
        Assert.Equal("Classifica e responde", output.Description);
        Assert.True(output.IsActive);
        Assert.Equal(
            [new WorkflowNodeOutput("a", a.AgentId, "Extraia os factos", 10, 20), new WorkflowNodeOutput("b", b.AgentId, null, 300, 20)],
            output.Nodes);
        Assert.Equal([new WorkflowEdgeOutput("a", "b")], output.Edges);
        Assert.NotEqual(default, output.CreatedAt);
        Assert.NotEqual(default, output.UpdatedAt);
    }

    public static TheoryData<string, Func<Guid, object>, string> InvalidGraphs() => new()
    {
        { "ciclo A→B→A", id => Body([Node("a", id), Node("b", id)], [Edge("a", "b"), Edge("b", "a")]), "edges" },
        { "laço A→A", id => Body([Node("a", id)], [Edge("a", "a")]), "edges" },
        { "aresta para key inexistente", id => Body([Node("a", id)], [Edge("a", "z")]), "edges" },
        { "key repetida", id => Body([Node("a", id), Node("a", id)], []), "nodes" },
        { "0 nós", _ => Body([], []), "nodes" },
        { "11 nós", id => Body(Enumerable.Range(0, 11).Select(i => Node($"n{i}", id)).ToArray(), []), "nodes" },
        { "31 arestas", id => Body(
            Enumerable.Range(0, 10).Select(i => Node($"n{i}", id)).ToArray(),
            Enumerable.Range(0, 10).SelectMany(i => Enumerable.Range(i + 1, 9 - i).Select(j => Edge($"n{i}", $"n{j}"))).Take(31).ToArray()), "edges" },
        { "aresta repetida", id => Body([Node("a", id), Node("b", id)], [Edge("a", "b"), Edge("a", "b")]), "edges" },
        { "name vazio", id => Body([Node("a", id)], [], name: ""), "name" },
        { "name 201", id => Body([Node("a", id)], [], name: new string('n', 201)), "name" },
        { "instruction 2001", id => Body([Node("a", id, new string('i', 2001))], []), "nodes[0].instruction" },
        { "ciclo A→B→C→A", id => Body([Node("a", id), Node("b", id), Node("c", id)], [Edge("a", "b"), Edge("b", "c"), Edge("c", "a")]), "edges" },
    };

    [Theory]
    [MemberData(nameof(InvalidGraphs))]
    public async Task Post_ShouldReturn400_ForInvalidGraph(string caseName, Func<Guid, object> body, string key)
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync(Base, body(agent.AgentId));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{caseName}: got {(int)response.StatusCode}");
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.True(problem.Errors.ContainsKey(key), $"{caseName}: keys {string.Join(",", problem.Errors.Keys)}");
    }

    [Fact]
    public async Task Post_ShouldAcceptDiamond()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync(Base, Body(
            [Node("a", agent.AgentId), Node("b", agent.AgentId), Node("c", agent.AgentId), Node("d", agent.AgentId)],
            [Edge("a", "b"), Edge("a", "c"), Edge("b", "d"), Edge("c", "d")]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public void Graph_ShouldDetectCycles_OnlyWhenPresent()
    {
        Assert.NotNull(WorkflowGraph.FindCycle(["a"], [("a", "a")]));
        Assert.NotNull(WorkflowGraph.FindCycle(["a", "b"], [("a", "b"), ("b", "a")]));
        Assert.NotNull(WorkflowGraph.FindCycle(["a", "b", "c"], [("a", "b"), ("b", "c"), ("c", "a")]));
        Assert.Null(WorkflowGraph.FindCycle(["a", "b", "c"], [("a", "b"), ("b", "c")]));
        Assert.Null(WorkflowGraph.FindCycle(["a", "b", "c", "d"], [("a", "b"), ("a", "c"), ("b", "d"), ("c", "d")]));
        Assert.Null(WorkflowGraph.FindCycle(["a", "b", "c"], []));
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("inactivo")]
    public async Task Post_ShouldReturn400_WhenAgentUnknownOrInactive(string caseName)
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agentId = Guid.NewGuid();
        if (caseName == "inactivo")
        {
            agentId = (await AiHttp.CreateAgentAsync(client)).AgentId;
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/ai/agents/{agentId}")).StatusCode);
        }

        var response = await client.PostAsJsonAsync(Base, Body([Node("a", agentId)], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.True(problem.Errors.ContainsKey("nodes"));
    }

    // ---- update ----------------------------------------------------------------------------

    [Fact]
    public async Task Put_ShouldReplaceGraph_AndAdvanceUpdatedAt()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, a, b) = await CreateChainAsync(client);

        var response = await client.PutAsJsonAsync($"{Base}/{workflow.WorkflowId}", new
        {
            name = "Renomeado",
            description = "nova",
            nodes = new[] { Node("a", a.AgentId), Node("c", b.AgentId, "resuma") },
            edges = new[] { Edge("a", "c") }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WorkflowOutput>())!;
        Assert.Equal("Renomeado", updated.Name);
        Assert.Equal("nova", updated.Description);
        Assert.Equal(["a", "c"], updated.Nodes.Select(n => n.Key));
        Assert.Equal([new WorkflowEdgeOutput("a", "c")], updated.Edges);
        Assert.True(updated.UpdatedAt > workflow.UpdatedAt);

        var stored = (await client.GetFromJsonAsync<WorkflowOutput>($"{Base}/{workflow.WorkflowId}"))!;
        Assert.Equal(["a", "c"], stored.Nodes.Select(n => n.Key));
        Assert.Equal([new WorkflowEdgeOutput("a", "c")], stored.Edges);
    }

    [Fact]
    public async Task Put_ShouldReturn400_AndKeepGraph_WhenCycle()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, a, b) = await CreateChainAsync(client);

        var response = await client.PutAsJsonAsync($"{Base}/{workflow.WorkflowId}", Body(
            [Node("a", a.AgentId), Node("b", b.AgentId)],
            [Edge("a", "b"), Edge("b", "a")]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.True(problem.Errors.ContainsKey("edges"));
        var stored = (await client.GetFromJsonAsync<WorkflowOutput>($"{Base}/{workflow.WorkflowId}"))!;
        Assert.Equal(workflow.Edges, stored.Edges);
        Assert.Equal(workflow.Name, stored.Name);
    }

    // ---- read ------------------------------------------------------------------------------

    [Fact]
    public async Task List_ShouldReturnActiveOnly_NewestUpdatedFirst()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var first = await CreateAsync(client, [Node("a", agent.AgentId)], name: "Primeiro");
        var second = await CreateAsync(client, [Node("a", agent.AgentId), Node("b", agent.AgentId)], name: "Segundo");
        var gone = await CreateAsync(client, [Node("a", agent.AgentId)], name: "Desativado");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Base}/{gone.WorkflowId}")).StatusCode);
        var touched = await client.PutAsJsonAsync($"{Base}/{first.WorkflowId}", Body([Node("a", agent.AgentId)], [], name: "Primeiro"));
        Assert.Equal(HttpStatusCode.OK, touched.StatusCode);

        var page = (await client.GetFromJsonAsync<PaginatedListOutput<WorkflowSummaryOutput>>(Base))!;

        Assert.Equal([first.WorkflowId, second.WorkflowId], page.Data.Select(w => w.WorkflowId));
        var top = page.Data[0];
        Assert.Equal("Primeiro", top.Name);
        Assert.Equal(1, top.NodeCount);
        Assert.True(top.IsActive);
        Assert.Equal(2, page.Data[1].NodeCount);
        Assert.True(top.UpdatedAt > page.Data[1].UpdatedAt);
    }

    [Fact]
    public async Task Get_ShouldReturnPositionsAsSaved()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var workflow = await CreateAsync(client, [Node("a", agent.AgentId, x: 12.5, y: -40.25)]);

        var stored = (await client.GetFromJsonAsync<WorkflowOutput>($"{Base}/{workflow.WorkflowId}"))!;

        var node = Assert.Single(stored.Nodes);
        Assert.Equal(12.5, node.X);
        Assert.Equal(-40.25, node.Y);
    }

    [Fact]
    public async Task Delete_ShouldDeactivate_AndHideFromList()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);

        var response = await client.DeleteAsync($"{Base}/{workflow.WorkflowId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = (await client.GetFromJsonAsync<WorkflowOutput>($"{Base}/{workflow.WorkflowId}"))!;
        Assert.False(stored.IsActive);
        var page = (await client.GetFromJsonAsync<PaginatedListOutput<WorkflowSummaryOutput>>(Base))!;
        Assert.DoesNotContain(page.Data, w => w.WorkflowId == workflow.WorkflowId);
    }

    // ---- boundaries ------------------------------------------------------------------------

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("POST runs")]
    public async Task Routes_ShouldReturn404_ForUnknownWorkflow(string route)
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var id = Guid.NewGuid();

        var response = route switch
        {
            "GET" => await client.GetAsync($"{Base}/{id}"),
            "PUT" => await client.PutAsJsonAsync($"{Base}/{id}", Body([Node("a", agent.AgentId)], [])),
            "DELETE" => await client.DeleteAsync($"{Base}/{id}"),
            _ => await client.PostAsJsonAsync($"{Base}/{id}/runs", new { input = "hello" })
        };

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Repository_ShouldNotFindWorkflow_OfAnotherTenant()
    {
        await using var factory = Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var (workflow, _, _) = await CreateChainAsync(client);

        await using var scope = factory.Services.CreateAsyncScope();
        ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).SetTenant(Guid.NewGuid());
        var repository = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();

        Assert.Null(await repository.GetByIdAsync(workflow.WorkflowId));
    }

    public static TheoryData<string, string> AllRoutes() => new()
    {
        { "POST", Base },
        { "GET", Base },
        { "GET", $"{Base}/{{id}}" },
        { "PUT", $"{Base}/{{id}}" },
        { "DELETE", $"{Base}/{{id}}" },
        { "POST", $"{Base}/{{id}}/runs" },
        { "GET", $"{Base}/{{id}}/runs" },
        { "GET", $"{Base}/{{id}}/runs/{{runId}}" },
    };

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Routes_ShouldReturn403_WithoutPermission(string method, string template)
    {
        await using var factory = Factory();
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await SendAsync(client, method, template, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Routes_ShouldReturn401_WithoutToken(string method, string template)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await SendAsync(client, method, template, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Routes_ShouldRequireDeclaredPolicy()
    {
        await using var factory = Factory();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        string PolicyOf(string method, string pattern) =>
            endpoints.Single(e =>
                    e.RoutePattern.RawText == pattern &&
                    e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method))
                .Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).Single(p => p is not null)!;

        const string one = Base + "/{workflowId:guid}";
        Assert.Equal(SecurityPolicies.AiAgentsManage, PolicyOf("POST", Base));
        Assert.Equal(SecurityPolicies.AiAgentsManage, PolicyOf("PUT", one));
        Assert.Equal(SecurityPolicies.AiAgentsManage, PolicyOf("DELETE", one));
        Assert.Equal(SecurityPolicies.AiAgentsManage, PolicyOf("POST", one + "/runs"));
        Assert.Equal(SecurityPolicies.AiAgentsRead, PolicyOf("GET", Base));
        Assert.Equal(SecurityPolicies.AiAgentsRead, PolicyOf("GET", one));
        Assert.Equal(SecurityPolicies.AiAgentsRead, PolicyOf("GET", one + "/runs"));
        Assert.Equal(SecurityPolicies.AiAgentsRead, PolicyOf("GET", one + "/runs/{runId:guid}"));
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Routes_ShouldReturn404_WhenAiDisabled(string method, string template)
    {
        await using var factory = Factory(configure: settings => settings["FeatureFlags:EnableAI"] = "false");
        using var client = await AiHttp.AdminClientAsync(factory);
        // A workflow and a run that exist, so a 404 can only come from the flag.
        var (workflowId, runId) = await InTenantAsync(factory, async services =>
        {
            var workflow = Workflow.Create(TenantTestDefaults.DevelopmentTenantId, "w", null, [new WorkflowNode("a", Guid.NewGuid(), null, 0, 0)], []);
            await services.GetRequiredService<IWorkflowRepository>().AddAsync(workflow);
            var run = WorkflowRun.Create(TenantTestDefaults.DevelopmentTenantId, workflow, "hello", new RunPrincipal(null, [], []));
            await services.GetRequiredService<IWorkflowRunRepository>().AddAsync(run);
            await services.GetRequiredService<AppDbContext>().SaveChangesAsync();
            return (workflow.Id, run.Id);
        });

        var response = await SendAsync(client, method, template, workflowId, runId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- storage ---------------------------------------------------------------------------

    [Fact]
    public async Task Model_ShouldDeclareOneWayConstraints()
    {
        await using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        static bool HasUniqueIndex(Microsoft.EntityFrameworkCore.Metadata.IEntityType type, params string[] columns) =>
            type.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(columns));

        var node = model.FindEntityType(typeof(WorkflowNode))!;
        Assert.Equal("AiWorkflowNodes", node.GetTableName());
        Assert.True(HasUniqueIndex(node, "WorkflowId", "Key"));
        var agentFk = Assert.Single(node.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Agent));
        Assert.Equal(DeleteBehavior.Restrict, agentFk.DeleteBehavior);

        var edge = model.FindEntityType(typeof(WorkflowEdge))!;
        Assert.Equal("AiWorkflowEdges", edge.GetTableName());
        Assert.True(HasUniqueIndex(edge, "WorkflowId", "FromKey", "ToKey"));

        var step = model.FindEntityType(typeof(WorkflowRunStep))!;
        Assert.Equal("AiWorkflowRunSteps", step.GetTableName());
        Assert.True(HasUniqueIndex(step, "WorkflowRunId", "NodeKey"));

        var run = model.FindEntityType(typeof(WorkflowRun))!;
        var workflowFk = Assert.Single(run.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Workflow));
        Assert.Equal(DeleteBehavior.Restrict, workflowFk.DeleteBehavior);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static object Body(object[] nodes, object[] edges, string name = "Workflow") =>
        new { name, description = (string?)null, nodes, edges };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string template, Guid id, Guid runId)
    {
        var url = template.Replace("{id}", id.ToString()).Replace("{runId}", runId.ToString());
        return method switch
        {
            "GET" => client.GetAsync(url),
            "DELETE" => client.DeleteAsync(url),
            "PUT" => client.PutAsJsonAsync(url, Body([Node("a", Guid.NewGuid())], [])),
            _ when url.EndsWith("/runs") => client.PostAsJsonAsync(url, new { input = "hello" }),
            _ => client.PostAsJsonAsync(url, Body([Node("a", Guid.NewGuid())], []))
        };
    }
}
