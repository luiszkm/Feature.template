using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Ai;
using Api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

public sealed class ListModelsTests
{
    private const string CatalogBody = """
        {"data":[
          {"id":"z/tools","name":"Z Tools","context_length":8192,
           "pricing":{"prompt":"0.000002","completion":"0.000004"},
           "supported_parameters":["temperature","tools"]},
          {"id":"a/tools","name":"A Tools","context_length":128000,
           "pricing":{"prompt":"0.0000001","completion":"0.0000003"},
           "supported_parameters":["tools","tool_choice"]},
          {"id":"m/no-tools","name":"No Tools","context_length":4096,
           "pricing":{"prompt":"0.000001","completion":"0.000001"},
           "supported_parameters":["temperature"]}
        ]}
        """;

    [Fact]
    public async Task OpenRouter_ShouldReturnOnlyToolModels_SortedById()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, CatalogBody));
        await using var factory = WithCatalog(OpenRouterCatalog(handler));
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(new[] { "a/tools", "z/tools" }, items.Select(i => i.GetProperty("id").GetString()).ToArray());
        var first = items[0];
        Assert.Equal(
            new[] { "contextLength", "id", "inputPricePerToken", "name", "outputPricePerToken" },
            first.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("A Tools", first.GetProperty("name").GetString());
        Assert.Equal(128000, first.GetProperty("contextLength").GetInt32());
        Assert.Equal(0.0000001m, first.GetProperty("inputPricePerToken").GetDecimal());
        Assert.Equal(0.0000003m, first.GetProperty("outputPricePerToken").GetDecimal());
        Assert.EndsWith("/models", handler.RequestUris.Single()!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenRouter_ShouldIntersectWithAllowedModels()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, CatalogBody));
        var catalog = OpenRouterCatalog(handler, allowed: ["z/tools", "m/no-tools", "missing/model"]);

        var models = await catalog.ListAsync();

        Assert.Equal(new[] { "z/tools" }, models.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Maf_ShouldReturnConfiguredModels_WithNullPrices()
    {
        var catalog = new ConfiguredModelCatalog(Options.Create(new LlmOptions
        {
            Provider = LlmProviders.MicrosoftAgentFramework,
            Model = "gpt-4o-mini",
            AllowedModels = ["gpt-4o", "gpt-4o-mini"]
        }));

        var models = await catalog.ListAsync();

        Assert.Equal(new[] { "gpt-4o", "gpt-4o-mini" }, models.Select(m => m.Id).ToArray());
        Assert.All(models, m =>
        {
            Assert.Null(m.InputPricePerToken);
            Assert.Null(m.OutputPricePerToken);
        });
    }

    [Fact]
    public async Task Get_ShouldReturnStubCatalog_InTesting()
    {
        await using var factory = AiHttp.Factory(settings => settings["Ai:Llm:Model"] = "cfg/model");
        using var client = await AiHttp.AdminClientAsync(factory);

        var models = await client.GetFromJsonAsync<List<ModelOutput>>("/api/v1/ai/models");

        Assert.Equal(
            new[] { "cfg/model", StubModelCatalog.ModelA, StubModelCatalog.ModelB }.Order(StringComparer.Ordinal).ToArray(),
            models!.Select(m => m.Id).ToArray());
        Assert.All(models, m =>
        {
            Assert.Equal(0m, m.InputPricePerToken);
            Assert.Equal(0m, m.OutputPricePerToken);
        });
    }

    [Fact]
    public async Task Get_ShouldReturn503_WhenProviderCatalogFails()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.BadGateway, "{}"));
        await using var factory = WithCatalog(OpenRouterCatalog(handler));
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/models");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Service unavailable", problem!.Title);
    }

    [Fact]
    public async Task OpenRouter_ShouldCacheSuccess()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, CatalogBody));
        var catalog = OpenRouterCatalog(handler);

        await catalog.ListAsync();
        await catalog.ListAsync();

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OpenRouter_ShouldNotCacheFailure()
    {
        var handler = new QueuedHttpHandler(
            (HttpStatusCode.InternalServerError, "{}"),
            (HttpStatusCode.OK, CatalogBody));
        var catalog = OpenRouterCatalog(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => catalog.ListAsync());
        var models = await catalog.ListAsync();

        Assert.Equal(2, handler.Calls);
        Assert.Equal(2, models.Count);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WithoutAgentRead()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/models");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    internal static OpenRouterModelCatalog OpenRouterCatalog(HttpMessageHandler handler, List<string>? allowed = null) =>
        new(
            new FixedHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.test/api/v1/") }),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new LlmOptions { Provider = LlmProviders.OpenRouter, AllowedModels = allowed ?? [] }),
            NullLogger<OpenRouterModelCatalog>.Instance);

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithCatalog(IModelCatalog catalog) =>
        AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(catalog)));
}
