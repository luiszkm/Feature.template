using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

public sealed class OpenRouterLlmServiceTests
{
    [Fact]
    public async Task CompleteAsync_ShouldPostChatCompletions_ToBaseUrl()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {"choices":[{"message":{"role":"assistant","content":"hi"}}],"usage":{"total_tokens":3}}
            """);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.test/api/v1/") };
        var sut = new OpenRouterLlmService(
            new FixedHttpClientFactory(http),
            Options.Create(new LlmOptions
            {
                Provider = LlmProviders.OpenRouter,
                ApiKey = "or-key",
                Model = "openai/gpt-4o-mini",
                BaseUrl = "https://openrouter.test/api/v1"
            }));

        var response = await sut.CompleteAsync(new LlmRequest("hello"), CancellationToken.None);

        Assert.Equal("hi", response.Text);
        Assert.NotNull(handler.RequestUri);
        Assert.StartsWith("https://openrouter.test/api/v1/chat/completions", handler.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Method);
    }

    [Fact]
    public async Task OpenRouter_ShouldSendConfiguredModel_WhenRequestModelIsNull()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, ChatBody));
        var sut = OpenRouter(handler, model: "openai/gpt-4o-mini");

        await sut.CompleteAsync(new LlmRequest("hello"), CancellationToken.None);

        Assert.Equal("openai/gpt-4o-mini", ModelOf(handler.RequestBodies.Single()));
    }

    [Fact]
    public async Task OpenRouter_ShouldSendRequestModel_WhenSet()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, ChatBody));
        var sut = OpenRouter(handler, model: "openai/gpt-4o-mini");

        await sut.CompleteAsync(new LlmRequest("hello", Model: "a/b"), CancellationToken.None);

        Assert.Equal("a/b", ModelOf(handler.RequestBodies.Single()));
    }

    [Fact]
    public async Task OpenRouter_ShouldMapUsageAndCost()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, """
            {"choices":[{"message":{"role":"assistant","content":"hi"}}],
             "usage":{"prompt_tokens":120,"completion_tokens":30,"total_tokens":150,"cost":0.00042}}
            """));
        var sut = OpenRouter(handler);

        var response = await sut.CompleteAsync(new LlmRequest("hello"), CancellationToken.None);

        Assert.Equal(120, response.InputTokens);
        Assert.Equal(30, response.OutputTokens);
        Assert.Equal(0.00042m, response.Cost);
    }

    [Fact]
    public async Task OpenRouter_ShouldReturnNullCost_WhenCostMissing()
    {
        var handler = new QueuedHttpHandler((HttpStatusCode.OK, """
            {"choices":[{"message":{"role":"assistant","content":"hi"}}],
             "usage":{"prompt_tokens":1,"completion_tokens":2,"total_tokens":3}}
            """));
        var sut = OpenRouter(handler);

        var response = await sut.CompleteAsync(new LlmRequest("hello"), CancellationToken.None);

        Assert.Null(response.Cost);
    }

    private const string ChatBody = """
        {"choices":[{"message":{"role":"assistant","content":"hi"}}],"usage":{"total_tokens":3}}
        """;

    private static OpenRouterLlmService OpenRouter(HttpMessageHandler handler, string model = "openai/gpt-4o-mini") =>
        new(
            new FixedHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.test/api/v1/") }),
            Options.Create(new LlmOptions
            {
                Provider = LlmProviders.OpenRouter,
                ApiKey = "or-key",
                Model = model,
                BaseUrl = "https://openrouter.test/api/v1"
            }));

    private static string? ModelOf(string json) =>
        System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("model").GetString();
}

public sealed class MicrosoftAgentFrameworkLlmServiceTests
{
    [Fact]
    public async Task CompleteAsync_ShouldSendHttp_ToBaseUrl()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "choices": [{"index":0,"message":{"role":"assistant","content":"from-maf"},"finish_reason":"stop"}],
              "usage": {"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}
            }
            """);
        var http = new HttpClient(handler);
        var sut = new MicrosoftAgentFrameworkLlmService(
            new FixedHttpClientFactory(http),
            Options.Create(new LlmOptions
            {
                Provider = LlmProviders.MicrosoftAgentFramework,
                ApiKey = "sk-test",
                Model = "gpt-4o-mini",
                BaseUrl = "https://maf.test/v1"
            }));

        var response = await sut.CompleteAsync(new LlmRequest("hello"), CancellationToken.None);

        Assert.Equal("from-maf", response.Text);
        Assert.NotNull(handler.RequestUri);
        Assert.Contains("maf.test", handler.RequestUri!.Host, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Post, handler.Method);
    }

    [Fact]
    public async Task Maf_ShouldMapInputOutputTokens_WithNullCost()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "id": "chatcmpl-2",
              "object": "chat.completion",
              "choices": [{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
              "usage": {"prompt_tokens":40,"completion_tokens":7,"total_tokens":47}
            }
            """);
        var sut = new MicrosoftAgentFrameworkLlmService(
            new FixedHttpClientFactory(new HttpClient(handler)),
            Options.Create(new LlmOptions
            {
                Provider = LlmProviders.MicrosoftAgentFramework,
                ApiKey = "sk-test",
                Model = "gpt-4o-mini",
                BaseUrl = "https://maf.test/v1"
            }));

        var response = await sut.CompleteAsync(new LlmRequest("hello"), CancellationToken.None);

        Assert.Equal(40, response.InputTokens);
        Assert.Equal(7, response.OutputTokens);
        Assert.Null(response.Cost);
    }
}

public sealed class LlmServiceRegistrationTests
{
    [Fact]
    public void ShouldRegisterStub_WhenTesting()
    {
        var sp = Build(Environments.Development, apiKey: "secret", environmentName: "Testing");
        Assert.IsType<StubLlmService>(sp.GetRequiredService<ILlmService>());
    }

    [Fact]
    public void ShouldRegisterStub_WhenDevelopmentAndApiKeyEmpty()
    {
        var sp = Build(Environments.Development, apiKey: "");
        Assert.IsType<StubLlmService>(sp.GetRequiredService<ILlmService>());
    }

    [Fact]
    public async Task ShouldFailFast_WhenProductionEnableAiAndApiKeyEmpty()
    {
        using var host = new HostBuilder()
            .UseEnvironment(Environments.Production)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FeatureFlags:EnableAI"] = "true",
                    ["Ai:Llm:Provider"] = LlmProviders.OpenRouter,
                    ["Ai:Llm:ApiKey"] = "",
                    ["Ai:Llm:Model"] = "openai/gpt-4o-mini"
                });
            })
            .ConfigureServices((_, services) => services.AddAiModule())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
        Assert.Contains("Ai:Llm:ApiKey", ex.Message, StringComparison.Ordinal);
    }

    private static IServiceProvider Build(string hostEnv, string apiKey, string? environmentName = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new StubHostEnvironment(environmentName ?? hostEnv));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:EnableAI"] = "true",
                ["Ai:Llm:Provider"] = LlmProviders.OpenRouter,
                ["Ai:Llm:ApiKey"] = apiKey,
                ["Ai:Llm:Model"] = "openai/gpt-4o-mini"
            })
            .Build());
        services.AddAiModule();
        return services.BuildServiceProvider();
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public Uri? RequestUri { get; private set; }
    public HttpMethod? Method { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        Method = request.Method;
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });
    }
}

internal sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
