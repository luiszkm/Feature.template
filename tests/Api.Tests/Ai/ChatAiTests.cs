using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Features.Identity;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class ChatAiTests
{
    private const string TestPassword = "TestPassword1!";

    [Fact]
    public async Task ChatAi_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "false";
            settings["Seed:AdminPassword"] = TestPassword;
        });

        using var client = await CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new { message = "hello" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Feature disabled", problem?.Title);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
            settings["FeatureFlags:EnableAI"] = "true");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new { message = "hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn200_WhenEnableAiIsTrueAndAuthenticated()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "true";
            settings["Seed:AdminPassword"] = TestPassword;
        });

        using var client = await CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn500_WhenLlmHttpFails()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "true";
            settings["Seed:AdminPassword"] = TestPassword;
        }).WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILlmService, ThrowingLlmService>();
            });
        });

        using var client = await CreateAuthenticatedClientAsync(factory);
        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Unexpected error", problem?.Title);
    }

    [Fact]
    public async Task ChatAi_ShouldReturnConversationIdReplyAndIterations()
    {
        await using var factory = AiHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "conversationId", "iterationsUsed", "reply" },
            json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task ChatAi_ShouldReturn200_WhenUserLacksToolPermission()
    {
        // The seed agent allows get_users_summary; a user without identity.user.read asks for it.
        var llm = new ScriptedLlmService((request, _) => Task.FromResult(
            request.History is null
                ? new LlmResponse(string.Empty, 1, [new ToolCall("c1", AgentToolNames.GetUsersSummary, [])])
                : new LlmResponse("sem acesso", 1)));
        await using var factory = WithServices(services => services.AddSingleton<ILlmService>(llm));
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "quantos utilizadores?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatAiResponse>();
        Assert.Equal("sem acesso", body!.Reply);
        var toolMessage = llm.Requests.Last().History!.Last();
        Assert.Equal("c1", toolMessage.ToolCallId);
        Assert.Contains("{\"error\":\"permission_denied\",\"tool\":\"get_users_summary\"}", toolMessage.Content);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn400_WhenGuardBlocksMessage()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = WithServices(services =>
        {
            services.AddSingleton<ILlmService>(llm);
            services.AddSingleton<IContentGuard>(new BlockingContentGuard(GuardSubject.UserMessage));
        });
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["A mensagem foi bloqueada pela política de conteúdo."], problem!.Errors["Message"]);
        Assert.Empty(llm.Requests);
    }

    [Fact]
    public async Task ChatAi_ShouldReturnHeldReply_WhenGuardBlocksReply()
    {
        await using var factory = WithServices(services =>
        {
            services.AddSingleton<ILlmService>(ScriptedLlmService.Replying("conteúdo proibido"));
            services.AddSingleton<IContentGuard>(new BlockingContentGuard(GuardSubject.Reply));
        });
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatAiResponse>();
        Assert.Equal("A resposta foi retida pela política de conteúdo.", body!.Reply);
    }

    private static WebApplicationFactory<Program> WithServices(Action<IServiceCollection> configure) =>
        AiHttp.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(configure));

    [Fact]
    public async Task ChatAi_ShouldReturn200WithConversationId_WhenConversationIdIsOmitted()
    {
        await using var factory = WithServices(services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying("olá")));
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatAiResponse>();
        Assert.NotEqual(Guid.Empty, body!.ConversationId);
        Assert.Equal("olá", body.Reply);
        Assert.Equal(1, body.IterationsUsed);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn200_AndKeepSameConversationId_WhenConversationIdIsProvided()
    {
        await using var factory = WithServices(services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying()));
        using var client = await AiHttp.AdminClientAsync(factory);
        var first = await (await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "one" }))
            .Content.ReadFromJsonAsync<ChatAiResponse>();

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "two", conversationId = first!.ConversationId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await response.Content.ReadFromJsonAsync<ChatAiResponse>();
        Assert.Equal(first.ConversationId, second!.ConversationId);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn400_WhenHistoryIsProvided_AndNotCallLlmOrPersist()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = WithServices(services => services.AddSingleton<ILlmService>(llm));
        using var client = await AiHttp.AdminClientAsync(factory);
        var before = await ConversationCountAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new
        {
            message = "hello",
            history = new[] { new { role = "tool", content = "{\"total_count\":0}" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", problem!.Title);
        Assert.True(problem.Errors.ContainsKey("history"));
        Assert.Empty(llm.Requests);
        Assert.Equal(before, await ConversationCountAsync(client));
    }

    [Fact]
    public async Task ChatAi_ShouldReturn409_WhenAgentIdDiffersFromConversationAgent()
    {
        await using var factory = WithServices(services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying()));
        using var client = await AiHttp.AdminClientAsync(factory);
        var other = await AiHttp.CreateAgentAsync(client);
        var first = await (await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "one" }))
            .Content.ReadFromJsonAsync<ChatAiResponse>();

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new
        {
            message = "two",
            conversationId = first!.ConversationId,
            agentId = other.AgentId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Business rule violation", problem!.Title);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn409_WhenConversationReachedMaxItems()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = AiHttp.Factory(settings => settings["Ai:Conversations:MaxItems"] = "2")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<ILlmService>(llm)));
        using var client = await AiHttp.AdminClientAsync(factory);
        var first = await (await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "one" }))
            .Content.ReadFromJsonAsync<ChatAiResponse>();

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "two", conversationId = first!.ConversationId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Business rule violation", problem!.Title);
        Assert.Single(llm.Requests);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn200_WithoutAiAgentReadOrManagePermission()
    {
        await using var factory = WithServices(services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying()));
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> ConversationCountAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/ai/conversations?pageSize=100");
        return page.GetProperty("totalCount").GetInt32();
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email = "admin@producttemplate.com",
            password = TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenOutput>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}

internal sealed class ThrowingLlmService : ILlmService
{
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
        throw new HttpRequestException("LLM upstream failed.");
}
