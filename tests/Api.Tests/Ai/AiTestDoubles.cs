using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Features.Identity;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Ai;

/// <summary>LLM double whose reply is decided per request; records every request it receives.</summary>
internal sealed class ScriptedLlmService(Func<LlmRequest, CancellationToken, Task<LlmResponse>> reply) : ILlmService
{
    public ConcurrentQueue<LlmRequest> Requests { get; } = new();

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return reply(request, cancellationToken);
    }

    public static ScriptedLlmService Replying(string text = "ok", int input = 1, int output = 1, decimal? cost = 0.001m) =>
        new((_, _) => Task.FromResult(new LlmResponse(text, input + output, InputTokens: input, OutputTokens: output, Cost: cost)));
}

/// <summary>Content guard that blocks every input of the given subjects and counts what it saw.</summary>
internal sealed class BlockingContentGuard(params GuardSubject[] blocked) : IContentGuard
{
    public ConcurrentQueue<GuardInput> Inputs { get; } = new();

    public Task<GuardVerdict> EvaluateAsync(GuardInput input, CancellationToken cancellationToken = default)
    {
        Inputs.Enqueue(input);
        return Task.FromResult(new GuardVerdict(blocked.Contains(input.Subject), "test"));
    }
}

internal sealed class FakeModelCatalog(params string[] ids) : IModelCatalog
{
    public Task<IReadOnlyList<ModelOutput>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ModelOutput>>(
            ids.Select(id => new ModelOutput(id, id, null, null, null)).ToList());
}

internal sealed class UnavailableModelCatalog : IModelCatalog
{
    public Task<IReadOnlyList<ModelOutput>> ListAsync(CancellationToken cancellationToken = default) =>
        throw new ServiceUnavailableException("Catálogo de modelos indisponível.");
}

/// <summary>HTTP handler answering from a queue of (status, body); counts calls and keeps request bodies.</summary>
internal sealed class QueuedHttpHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
{
    private int _next;

    public int Calls => _next;
    public List<string> RequestBodies { get; } = [];
    public List<Uri?> RequestUris { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri);
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        var index = Math.Min(Interlocked.Increment(ref _next) - 1, responses.Length - 1);
        var (status, body) = responses[index];
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
    }
}

internal static class AiHttp
{
    public const string TestPassword = "TestPassword1!";

    public static WebApplicationFactory<Program> Factory(Action<Dictionary<string, string?>>? configure = null) =>
        TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "true";
            settings["Seed:AdminPassword"] = TestPassword;
            configure?.Invoke(settings);
        });

    public static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");
        await LoginAsync(client, "admin@producttemplate.com");
        return client;
    }

    /// <summary>A freshly registered user with no role: neither ai.agent.read nor ai.agent.manage.</summary>
    public static async Task<HttpClient> PlainUserClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");
        var email = $"plain-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email,
            password = TestPassword,
            firstName = "Plain",
            lastName = "User"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await LoginAsync(client, email);
        return client;
    }

    public static async Task<AgentOutput> CreateAgentAsync(HttpClient client, string? model = null, string[]? tools = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = $"Agent {Guid.NewGuid():N}",
            instructions = "Answer briefly.",
            toolNames = tools ?? Array.Empty<string>(),
            model
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AgentOutput>())!;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new { email, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthTokenOutput>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }
}
