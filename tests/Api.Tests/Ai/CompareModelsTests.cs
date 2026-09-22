using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

public sealed class CompareModelsTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;
    private static readonly string[] TwoModels = [StubModelCatalog.ModelA, StubModelCatalog.ModelB];

    // ---- HTTP ------------------------------------------------------------------------------

    [Fact]
    public async Task Post_ShouldReturn201_WithOneResultPerModel_InRequestOrder()
    {
        // ModelB answers before ModelA: the response order must still follow the request.
        var llm = new ScriptedLlmService(async (request, ct) =>
        {
            if (request.Model == StubModelCatalog.ModelA)
                await Task.Delay(150, ct);
            return new LlmResponse($"from {request.Model}", 2, InputTokens: 1, OutputTokens: 1, Cost: 0.001m);
        });
        await using var factory = WithLlm(llm);
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = agent.AgentId,
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var output = await response.Content.ReadFromJsonAsync<ComparisonOutput>();
        Assert.Equal($"/api/v1/ai/comparisons/{output!.ComparisonId}", response.Headers.Location?.OriginalString);
        Assert.Equal(TwoModels, output.Results.Select(r => r.Model).ToArray());
        Assert.Equal($"from {StubModelCatalog.ModelA}", output.Results[0].Reply);
    }

    [Fact]
    public async Task Post_ShouldReturn201_AndPersist_WhenAllModelsFail()
    {
        await using var factory = WithLlm(new ScriptedLlmService((_, _) => throw new HttpRequestException("429")));
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = agent.AgentId,
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ComparisonOutput>();
        var stored = await client.GetFromJsonAsync<ComparisonOutput>($"/api/v1/ai/comparisons/{created!.ComparisonId}");
        Assert.Equal(2, stored!.Results.Count);
        Assert.All(stored.Results, r => Assert.Equal("Failed", r.Status));
    }

    public static TheoryData<string, object> InvalidBodies() => new()
    {
        { "1 modelo", new { prompt = "p", models = new[] { StubModelCatalog.ModelA } } },
        { "5 modelos", new { prompt = "p", models = new[] { "a", "b", "c", "d", "e" } } },
        { "ids repetidos", new { prompt = "p", models = new[] { StubModelCatalog.ModelA, StubModelCatalog.ModelA } } },
        { "id fora do catálogo", new { prompt = "p", models = new[] { StubModelCatalog.ModelA, "vendor/unknown" } } },
        { "prompt vazio", new { prompt = "", models = TwoModels } },
        { "prompt 4001", new { prompt = new string('p', 4001), models = TwoModels } },
        { "4 anexos", new { prompt = "p", models = TwoModels, attachments = Enumerable.Range(0, 4).Select(i => new { name = $"f{i}.txt", content = "x" }).ToArray() } },
        { "anexos 100001 chars", new { prompt = "p", models = TwoModels, attachments = new[] { new { name = "a.txt", content = new string('x', 50_000) }, new { name = "b.txt", content = new string('x', 50_001) } } } },
        { "name vazio", new { prompt = "p", models = TwoModels, attachments = new[] { new { name = "", content = "x" } } } },
        { "name 201", new { prompt = "p", models = TwoModels, attachments = new[] { new { name = new string('n', 201), content = "x" } } } },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Post_ShouldReturn400_WhenInputInvalid(string caseName, object body)
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var json = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        json["agentId"] = agent.AgentId.ToString();
        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", json);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{caseName}: got {(int)response.StatusCode}");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", problem!.Title);
    }

    [Fact]
    public async Task Post_ShouldReturn404_WhenAgentMissing()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = Guid.NewGuid(),
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ShouldReturn404_WhenAgentInactive()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/ai/agents/{agent.AgentId}")).StatusCode);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = agent.AgentId,
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenProviderIsMaf()
    {
        await using var factory = AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILlmService>(MafService())));
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = agent.AgentId,
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Business rule violation", problem!.Title);
        Assert.Equal("Comparação requer o provider OpenRouter", problem.Detail);
    }

    [Fact]
    public async Task Post_ShouldReturn503_WhenCatalogUnavailable()
    {
        await using var factory = AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IModelCatalog, UnavailableModelCatalog>()));
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = agent.AgentId,
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Service unavailable", problem!.Title);
    }

    [Fact]
    public async Task List_ShouldReturnTenantPage_NewestFirst()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying(cost: 0.002m));
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var longPrompt = new string('q', 300);
        var first = await PostComparisonAsync(client, agent.AgentId, "older");
        var second = await PostComparisonAsync(client, agent.AgentId, longPrompt);

        var response = await client.GetAsync("/api/v1/ai/comparisons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PaginatedListOutput<ComparisonSummaryOutput>>();
        Assert.Equal(1, page!.PageNumber);
        Assert.Equal(20, page.PageSize);
        var ours = page.Data.Where(c => c.AgentId == agent.AgentId).ToList();
        Assert.Equal(new[] { second.ComparisonId, first.ComparisonId }, ours.Select(c => c.ComparisonId).ToArray());
        var newest = ours[0];
        Assert.Equal(agent.Name, newest.AgentName);
        Assert.Equal(new string('q', 200), newest.PromptPreview);
        Assert.Equal(TwoModels, newest.Models.ToArray());
        Assert.Equal(0.004m, newest.TotalCost);
        Assert.True(newest.CreatedAt >= ours[1].CreatedAt);
    }

    [Fact]
    public async Task Get_ShouldReturnComparison()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying("answer", input: 4, output: 6, cost: 0.01m));
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);
        var created = await PostComparisonAsync(client, agent.AgentId, "hello", attachments: [new { name = "notes.md", content = "# n" }]);

        var response = await client.GetAsync($"/api/v1/ai/comparisons/{created.ComparisonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await response.Content.ReadFromJsonAsync<ComparisonOutput>();
        Assert.Equal(agent.AgentId, stored!.AgentId);
        Assert.Equal(agent.Name, stored.AgentName);
        Assert.Equal("hello", stored.Prompt);
        Assert.Equal(new[] { "notes.md" }, stored.Attachments.Select(a => a.Name).ToArray());
        Assert.Equal(0.02m, stored.TotalCost);
        Assert.Equal(TwoModels, stored.Results.Select(r => r.Model).ToArray());
        Assert.All(stored.Results, r =>
        {
            Assert.Equal("Succeeded", r.Status);
            Assert.Equal("answer", r.Reply);
            Assert.Equal(4, r.InputTokens);
            Assert.Equal(6, r.OutputTokens);
            Assert.Equal(0.01m, r.Cost);
        });
    }

    [Fact]
    public async Task Get_ShouldReturn404_ForOtherTenantOrMissing()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = await AiHttp.AdminClientAsync(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/ai/comparisons/{Guid.NewGuid()}")).StatusCode);

        var provider = HandlerProvider(nameof(Get_ShouldReturn404_ForOtherTenantOrMissing), ScriptedLlmService.Replying());
        var comparisonId = await RunComparisonAsync(provider, TwoModels);
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.ServiceProvider.GetRequiredService<GetModelComparisonHandler>()
                .Handle(new GetModelComparisonQuery(comparisonId), CancellationToken.None));
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = Guid.NewGuid(),
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ShouldReturn400_WhenGuardBlocksMessage()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILlmService>(llm);
                services.AddSingleton<IContentGuard>(new BlockingContentGuard(GuardSubject.UserMessage));
            }));
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = agent.AgentId,
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["A mensagem foi bloqueada pela política de conteúdo."], problem!.Errors["Message"]);
        Assert.Empty(llm.Requests);
    }

    [Fact]
    public async Task Post_ShouldReturn403_WithoutAgentManage()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId = Guid.NewGuid(),
            prompt = "hello",
            models = TwoModels
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WithoutAgentRead()
    {
        await using var factory = WithLlm(ScriptedLlmService.Replying());
        using var client = await AiHttp.PlainUserClientAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/ai/comparisons")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/ai/comparisons/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task NewRoutes_ShouldReturn404_WhenAiDisabled()
    {
        await using var factory = AiHttp.Factory(settings => settings["FeatureFlags:EnableAI"] = "false");
        using var client = await AiHttp.AdminClientAsync(factory);

        var responses = new[]
        {
            await client.GetAsync("/api/v1/ai/models"),
            await client.PostAsJsonAsync("/api/v1/ai/comparisons", new { agentId = Guid.NewGuid(), prompt = "p", models = TwoModels }),
            await client.GetAsync("/api/v1/ai/comparisons"),
            await client.GetAsync($"/api/v1/ai/comparisons/{Guid.NewGuid()}")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("Feature disabled", problem!.Title);
        }
    }

    // ---- Handler ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ShouldRunAgentPerModel_WithSameInstructionsToolsAndTemperature()
    {
        var llm = ScriptedLlmService.Replying();
        var provider = HandlerProvider(nameof(Handle_ShouldRunAgentPerModel_WithSameInstructionsToolsAndTemperature), llm);

        await RunComparisonAsync(provider, TwoModels, tools: [AgentToolNames.GetTenantInfo], instructions: "Be terse.");

        var requests = llm.Requests.ToArray();
        Assert.Equal(TwoModels.Order(), requests.Select(r => r.Model!).Order());
        Assert.All(requests, r =>
        {
            Assert.Equal("Be terse.\n\n" + AgentGuardrails.SystemSuffix, r.SystemPrompt);
            Assert.Equal(new[] { AgentToolNames.GetTenantInfo }, r.Tools?.Select(t => t.Name).ToArray());
            Assert.Equal(0.2f, r.Temperature);
        });
    }

    [Fact]
    public async Task Handle_ShouldAppendAttachmentsToPrompt_IdenticallyForAllModels()
    {
        var llm = ScriptedLlmService.Replying();
        var provider = HandlerProvider(nameof(Handle_ShouldAppendAttachmentsToPrompt_IdenticallyForAllModels), llm);

        await RunComparisonAsync(provider, TwoModels, prompt: "Summarise", attachments:
        [
            new ComparisonAttachmentInput("a.txt", "alpha"),
            new ComparisonAttachmentInput("b.csv", "x,y")
        ]);

        const string expected = "Summarise\n\n--- a.txt ---\nalpha\n\n--- b.csv ---\nx,y";
        Assert.All(llm.Requests, r => Assert.Equal(expected, r.UserPrompt));
        Assert.Equal(2, llm.Requests.Count);
    }

    [Fact]
    public async Task Handle_ShouldFillSucceededResult()
    {
        var provider = HandlerProvider(
            nameof(Handle_ShouldFillSucceededResult),
            ScriptedLlmService.Replying("fine", input: 11, output: 5, cost: 0.0007m));

        var output = await RunComparisonOutputAsync(provider, TwoModels);

        Assert.All(output.Results, r =>
        {
            Assert.Equal("Succeeded", r.Status);
            Assert.Equal("fine", r.Reply);
            Assert.Equal(11, r.InputTokens);
            Assert.Equal(5, r.OutputTokens);
            Assert.Equal(0.0007m, r.Cost);
            Assert.True(r.LatencyMs >= 0);
            Assert.Equal(1, r.IterationsUsed);
            Assert.Null(r.ErrorCode);
        });
    }

    [Fact]
    public async Task Handle_ShouldIsolateFailure_ToOneModel()
    {
        var llm = new ScriptedLlmService((request, _) => request.Model == StubModelCatalog.ModelA
            ? throw new HttpRequestException("429 Too Many Requests")
            : Task.FromResult(new LlmResponse("ok", 2, InputTokens: 1, OutputTokens: 1, Cost: 0.001m)));
        var provider = HandlerProvider(nameof(Handle_ShouldIsolateFailure_ToOneModel), llm);

        var output = await RunComparisonOutputAsync(provider, TwoModels);

        var failed = output.Results[0];
        Assert.Equal("Failed", failed.Status);
        Assert.Null(failed.Reply);
        Assert.Equal(nameof(HttpRequestException), failed.ErrorCode);
        Assert.Equal("Succeeded", output.Results[1].Status);
    }

    [Fact]
    public async Task Handle_ShouldMarkTimedOut_WhenModelExceedsTimeout()
    {
        var llm = new ScriptedLlmService(async (request, ct) =>
        {
            if (request.Model == StubModelCatalog.ModelB)
                await Task.Delay(Timeout.Infinite, ct);
            return new LlmResponse("ok", 2, InputTokens: 1, OutputTokens: 1, Cost: 0.001m);
        });
        var provider = HandlerProvider(
            nameof(Handle_ShouldMarkTimedOut_WhenModelExceedsTimeout),
            llm,
            options => options.CompareTimeoutSeconds = 1);

        var output = await RunComparisonOutputAsync(provider, TwoModels);

        Assert.Equal("Succeeded", output.Results[0].Status);
        Assert.Equal("TimedOut", output.Results[1].Status);
        Assert.Equal("Timeout", output.Results[1].ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldCompleteAndPersist_WhenCallerTokenIsCancelled()
    {
        using var caller = new CancellationTokenSource();
        var llm = new ScriptedLlmService(async (_, ct) =>
        {
            await caller.CancelAsync();
            await Task.Delay(50, ct);
            return new LlmResponse("still here", 2, InputTokens: 1, OutputTokens: 1, Cost: 0.001m);
        });
        var provider = HandlerProvider(nameof(Handle_ShouldCompleteAndPersist_WhenCallerTokenIsCancelled), llm);

        var output = await RunComparisonOutputAsync(provider, TwoModels, caller.Token);

        Assert.True(caller.IsCancellationRequested);
        Assert.All(output.Results, r => Assert.Equal("Succeeded", r.Status));
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IModelComparisonRepository>()
            .GetByIdAsync(output.ComparisonId));
    }

    [Fact]
    public async Task Handle_ShouldSumNonNullCosts()
    {
        var llm = new ScriptedLlmService((request, _) => Task.FromResult(new LlmResponse(
            "ok", 2, InputTokens: 1, OutputTokens: 1,
            Cost: request.Model == StubModelCatalog.ModelA ? 0.003m : null)));
        var provider = HandlerProvider(nameof(Handle_ShouldSumNonNullCosts), llm);

        var output = await RunComparisonOutputAsync(provider, TwoModels);

        Assert.Equal(0.003m, output.TotalCost);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullTotalCost_WhenAllCostsNull()
    {
        var provider = HandlerProvider(
            nameof(Handle_ShouldReturnNullTotalCost_WhenAllCostsNull),
            ScriptedLlmService.Replying(cost: null));

        var output = await RunComparisonOutputAsync(provider, TwoModels);

        Assert.Null(output.TotalCost);
    }

    [Fact]
    public async Task Handle_ShouldRecordUsageEntryPerModel()
    {
        var provider = HandlerProvider(nameof(Handle_ShouldRecordUsageEntryPerModel), ScriptedLlmService.Replying());

        await RunComparisonAsync(provider, TwoModels);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var entries = await scope.ServiceProvider.GetRequiredService<IAiUsageRepository>().ListAsync();
        var compare = entries.Where(e => e.Operation == AiUsageOperations.Compare).ToList();
        Assert.Equal(TwoModels.Order(), compare.Select(e => e.Model).Order());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRule_WhenProviderIsMaf()
    {
        var provider = HandlerProvider(nameof(Handle_ShouldThrowBusinessRule_WhenProviderIsMaf), MafService());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => RunComparisonAsync(provider, TwoModels));

        Assert.Equal("Comparação requer o provider OpenRouter", error.Message);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static WebApplicationFactory<Program> WithLlm(ILlmService llm) =>
        AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(llm)));

    private static async Task<ComparisonOutput> PostComparisonAsync(
        HttpClient client,
        Guid agentId,
        string prompt,
        object[]? attachments = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new
        {
            agentId,
            prompt,
            models = TwoModels,
            attachments = attachments ?? []
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ComparisonOutput>())!;
    }

    private static IServiceProvider HandlerProvider(
        string name,
        ILlmService llm,
        Action<LlmOptions>? configureOptions = null) =>
        TestServiceFactory.CreateWithAi(name, services =>
        {
            services.AddSingleton(llm);
            services.AddScoped<CompareModelsHandler>();
            services.AddScoped<GetModelComparisonHandler>();
            if (configureOptions is not null)
                services.Configure(configureOptions);
        });

    private static async Task<Guid> RunComparisonAsync(
        IServiceProvider provider,
        string[] models,
        string prompt = "hello",
        IReadOnlyList<ComparisonAttachmentInput>? attachments = null,
        string[]? tools = null,
        string instructions = "x") =>
        (await RunComparisonOutputAsync(provider, models, CancellationToken.None, prompt, attachments, tools, instructions)).ComparisonId;

    private static async Task<ComparisonOutput> RunComparisonOutputAsync(
        IServiceProvider provider,
        string[] models,
        CancellationToken cancellationToken = default,
        string prompt = "hello",
        IReadOnlyList<ComparisonAttachmentInput>? attachments = null,
        string[]? tools = null,
        string instructions = "x")
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId, "dev");
        var agent = await scope.ServiceProvider.GetRequiredService<CreateAgentHandler>().Handle(
            new CreateAgentCommand($"Cmp {Guid.NewGuid():N}", instructions, tools ?? []),
            CancellationToken.None);

        return await scope.ServiceProvider.GetRequiredService<CompareModelsHandler>().Handle(
            new CompareModelsCommand(agent.AgentId, prompt, models, attachments),
            cancellationToken);
    }

    private static MicrosoftAgentFrameworkLlmService MafService() =>
        new(
            new FixedHttpClientFactory(new HttpClient()),
            Options.Create(new LlmOptions
            {
                Provider = LlmProviders.MicrosoftAgentFramework,
                ApiKey = "sk-test",
                Model = "gpt-4o-mini",
                BaseUrl = "https://maf.test/v1"
            }));
}
