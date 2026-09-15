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
