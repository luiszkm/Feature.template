using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class AgentModelTests
{
    [Fact]
    public async Task Post_ShouldReturn201_WithModel_WhenModelInCatalog()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = $"Model {Guid.NewGuid():N}",
            instructions = "x",
            toolNames = Array.Empty<string>(),
            model = StubModelCatalog.ModelA
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<AgentOutput>();
        Assert.Equal(StubModelCatalog.ModelA, agent!.Model);
    }

    [Fact]
    public async Task Post_ShouldStoreNullModel_WhenModelOmitted()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = $"NoModel {Guid.NewGuid():N}",
            instructions = "x",
            toolNames = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AgentOutput>();
        Assert.Null(created!.Model);
        var stored = await client.GetFromJsonAsync<AgentOutput>($"/api/v1/ai/agents/{created.AgentId}");
        Assert.Null(stored!.Model);
    }

    [Fact]
    public async Task Post_ShouldReturn400_WhenModelNotInCatalog()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = $"Bad {Guid.NewGuid():N}",
            instructions = "x",
            toolNames = Array.Empty<string>(),
            model = "vendor/not-a-model"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", problem!.Title);
        Assert.Contains("Model", problem.Errors.Keys);
    }

    [Fact]
    public async Task Put_ShouldReturn400_WhenModelNotInCatalog()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PutAsJsonAsync($"/api/v1/ai/agents/{agent.AgentId}", new
        {
            name = agent.Name,
            instructions = agent.Instructions,
            toolNames = agent.ToolNames,
            model = "vendor/not-a-model"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", problem!.Title);
        Assert.Contains("Model", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_ShouldReturn400_WhenModelExceeds200Chars()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = $"Long {Guid.NewGuid():N}",
            instructions = "x",
            toolNames = Array.Empty<string>(),
            model = new string('m', 201)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", problem!.Title);
        Assert.Contains("Model", problem.Errors.Keys);
    }

    [Fact]
    public async Task Put_ShouldClearModel_WhenModelOmitted()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client, model: StubModelCatalog.ModelB);
        Assert.Equal(StubModelCatalog.ModelB, agent.Model);

        var response = await client.PutAsJsonAsync($"/api/v1/ai/agents/{agent.AgentId}", new
        {
            name = agent.Name,
            instructions = agent.Instructions,
            toolNames = agent.ToolNames
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await client.GetFromJsonAsync<AgentOutput>($"/api/v1/ai/agents/{agent.AgentId}");
        Assert.Null(stored!.Model);
    }

    [Fact]
    public async Task DefaultAgent_ShouldHaveNullModel()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(DefaultAgent_ShouldHaveNullModel));
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        await scope.ServiceProvider.GetRequiredService<IDefaultAgentProvisioner>()
            .EnsureDefaultAgentAsync(TenantTestDefaults.DevelopmentTenantId);

        var seed = await scope.ServiceProvider.GetRequiredService<IAgentRepository>().GetDefaultAsync();

        Assert.NotNull(seed);
        Assert.Null(seed.Model);
    }

    [Fact]
    public async Task GetAndList_ShouldReturnModel()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client, model: StubModelCatalog.ModelA);

        var single = await client.GetFromJsonAsync<AgentOutput>($"/api/v1/ai/agents/{agent.AgentId}");
        var page = await client.GetFromJsonAsync<PaginatedListOutput<AgentOutput>>("/api/v1/ai/agents?pageSize=100");

        Assert.Equal(StubModelCatalog.ModelA, single!.Model);
        Assert.Equal(StubModelCatalog.ModelA, Assert.Single(page!.Data, a => a.AgentId == agent.AgentId).Model);
    }

    [Fact]
    public async Task Post_ShouldReturn503_WhenCatalogUnavailable()
    {
        await using var factory = AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IModelCatalog, UnavailableModelCatalog>()));
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = $"Down {Guid.NewGuid():N}",
            instructions = "x",
            toolNames = Array.Empty<string>(),
            model = StubModelCatalog.ModelA
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Service unavailable", problem!.Title);
    }
}
