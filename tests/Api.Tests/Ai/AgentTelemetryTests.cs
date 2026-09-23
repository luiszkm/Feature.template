using System.Collections.Concurrent;
using System.Diagnostics;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace Api.Tests.Ai;

/// <summary>An ActivityListener is process-wide: these tests must not overlap each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AiTelemetryCollection
{
    public const string Name = "AiTelemetry";
}

[Collection(AiTelemetryCollection.Name)]
public sealed class AgentTelemetryTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldStartInvokeAgentSpan_WithGenAiAttributes()
    {
        using var capture = new SpanCapture();
        var provider = Provider(nameof(Handle_ShouldStartInvokeAgentSpan_WithGenAiAttributes), ScriptedLlmService.Replying());
        var agent = await DefaultAgentAsync(provider);
        var modelled = await CreateAgentAsync(provider, "Modelled", StubModelCatalog.ModelA);

        await ChatAsync(provider, new ChatAiCommand("hi"));
        await ChatAsync(provider, new ChatAiCommand("hi", AgentId: modelled));

        var spans = capture.Spans(AiTelemetry.InvokeAgent);
        var span = Assert.Single(spans, s => s.DisplayName == "invoke_agent Default");
        Assert.Equal("invoke_agent", span.GetTagItem("gen_ai.operation.name"));
        Assert.Equal("stub", span.GetTagItem("gen_ai.provider.name"));
        Assert.Equal("stub", span.GetTagItem("gen_ai.request.model"));
        Assert.Equal(agent.Id.ToString(), span.GetTagItem("gen_ai.agent.id"));
        Assert.Equal("Default", span.GetTagItem("gen_ai.agent.name"));
        // An agent with its own model: provider and model are distinct values, each from its own source.
        var own = Assert.Single(spans, s => s.DisplayName == "invoke_agent Modelled");
        Assert.Equal(StubModelCatalog.ModelA, own.GetTagItem("gen_ai.request.model"));
        Assert.Equal("stub", own.GetTagItem("gen_ai.provider.name"));
        Assert.Equal(modelled.ToString(), own.GetTagItem("gen_ai.agent.id"));
    }

    [Fact]
    public async Task InvokeAgentSpan_ShouldCarryConversationId()
    {
        using var capture = new SpanCapture();
        var provider = Provider(nameof(InvokeAgentSpan_ShouldCarryConversationId), ScriptedLlmService.Replying());

        var created = await ChatAsync(provider, new ChatAiCommand("first"));
        var appended = await ChatAsync(provider, new ChatAiCommand("second", ConversationId: created.ConversationId));

        var spans = capture.Spans(AiTelemetry.InvokeAgent);
        Assert.Equal(2, spans.Count);
        Assert.All(spans, span => Assert.Equal(created.ConversationId.ToString(), span.GetTagItem("gen_ai.conversation.id")));
        Assert.Equal(created.ConversationId, appended.ConversationId);
    }

    [Fact]
    public async Task RunAsync_ShouldStartChatSpan_PerLlmCall_IncludingSummaryFallback()
    {
        using var capture = new SpanCapture();
        var llm = new ScriptedLlmService((request, _) => Task.FromResult(
            request.Tools is { Count: > 0 }
                ? new LlmResponse(string.Empty, 5, [new ToolCall("c1", "t", [])], InputTokens: 3, OutputTokens: 2)
                : new LlmResponse("summary", 9, InputTokens: 7, OutputTokens: 2)));

        await Loop(llm, new LambdaTool("t", (_, _) => Task.FromResult("abc"))).RunAsync("go", "sys", null, ["t"], model: "m1");

        var chats = capture.Spans(AiTelemetry.Chat);
        Assert.Equal(6, chats.Count);
        Assert.All(chats, span =>
        {
            Assert.Equal("chat m1", span.DisplayName);
            Assert.Equal("chat", span.GetTagItem("gen_ai.operation.name"));
            Assert.Equal("m1", span.GetTagItem("gen_ai.request.model"));
        });
        Assert.Equal(5, chats.Count(s => Equals(s.GetTagItem("gen_ai.usage.input_tokens"), 3)));
        var summary = Assert.Single(chats, s => Equals(s.GetTagItem("gen_ai.usage.input_tokens"), 7));
        Assert.Equal(2, summary.GetTagItem("gen_ai.usage.output_tokens"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStartExecuteToolSpan_WithGenAiAttributes()
    {
        using var capture = new SpanCapture();

        await Loop(CallsToolOnce("t"), new LambdaTool("t", (_, _) => Task.FromResult("abc"))).RunAsync("go", "sys", null, ["t"]);

        var span = Assert.Single(capture.Spans(AiTelemetry.ExecuteTool));
        Assert.Equal("execute_tool t", span.DisplayName);
        Assert.Equal("execute_tool", span.GetTagItem("gen_ai.operation.name"));
        Assert.Equal("t", span.GetTagItem("gen_ai.tool.name"));
        Assert.Equal("c1", span.GetTagItem("gen_ai.tool.call.id"));
        Assert.Null(span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    [Theory]
    [InlineData("t", "other")]
    [InlineData("ghost", "ghost")]
    public async Task ExecuteAsync_ShouldSetToolNotFoundErrorType_WhenToolIsOutsideAllowlist(string called, string allowed)
    {
        using var capture = new SpanCapture();
        var llm = CallsToolOnce(called);

        var result = await Loop(llm, new LambdaTool("t", (_, _) => Task.FromResult("abc"))).RunAsync("go", "sys", null, [allowed]);

        var span = Assert.Single(capture.Spans(AiTelemetry.ExecuteTool));
        Assert.Equal("tool_not_found", span.GetTagItem("error.type"));
        Assert.Equal("done", result.Reply);
        Assert.Contains("tool_not_found", llm.Requests.Last().History!.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task ChatSpan_ShouldSetErrorTypeAndErrorStatus_WhenLlmThrows()
    {
        using var capture = new SpanCapture();

        await Assert.ThrowsAsync<HttpRequestException>(() => Loop(new ThrowingLlmService()).RunAsync("go", "sys", null, []));

        var span = Assert.Single(capture.Spans(AiTelemetry.Chat));
        Assert.Equal("HttpRequestException", span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public async Task ExecuteToolSpan_ShouldSetErrorTypeAndErrorStatus_WhenToolThrows()
    {
        using var capture = new SpanCapture();

        await Loop(CallsToolOnce("t"), new LambdaTool("t", (_, _) => throw new InvalidOperationException("boom")))
            .RunAsync("go", "sys", null, ["t"]);

        var span = Assert.Single(capture.Spans(AiTelemetry.ExecuteTool));
        Assert.Equal("InvalidOperationException", span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public async Task InvokeAgentSpan_ShouldSetErrorType_WhenChatFails()
    {
        using var capture = new SpanCapture();
        var provider = Provider(nameof(InvokeAgentSpan_ShouldSetErrorType_WhenChatFails), new ThrowingLlmService());

        await Assert.ThrowsAsync<HttpRequestException>(() => ChatAsync(provider, new ChatAiCommand("hi")));

        var span = Assert.Single(capture.Spans(AiTelemetry.InvokeAgent));
        Assert.Equal("HttpRequestException", span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public async Task Spans_ShouldNotContain_ContentAttributes()
    {
        using var capture = new SpanCapture();
        var provider = Provider(nameof(Spans_ShouldNotContain_ContentAttributes), CallsToolOnce(AgentToolNames.GetTenantInfo));

        await ChatAsync(provider, new ChatAiCommand("segredo do utilizador"));

        var spans = capture.Spans();
        Assert.Contains(spans, s => s.GetTagItem("gen_ai.operation.name") as string == "execute_tool");
        string[] forbidden = ["gen_ai.tool.call.arguments", "gen_ai.tool.call.result", "gen_ai.input.messages", "gen_ai.output.messages"];
        Assert.All(spans, span => Assert.Empty(span.TagObjects.Select(t => t.Key).Intersect(forbidden)));
        Assert.All(spans, span => Assert.DoesNotContain(span.TagObjects, t => t.Value?.ToString()?.Contains("segredo") == true));
    }

    [Theory]
    [InlineData(LlmProviders.OpenRouter, "openrouter")]
    [InlineData(LlmProviders.MicrosoftAgentFramework, "microsoft.agent_framework")]
    [InlineData("stub", "stub")]
    public void ProviderValue_ShouldMapEveryProviderLabel(string label, string expected)
    {
        Assert.Equal(expected, AiTelemetry.ProviderValue(label));
    }

    [Theory]
    [InlineData(LlmProviders.OpenRouter, "Production", "key", "openrouter")]
    [InlineData(LlmProviders.MicrosoftAgentFramework, "Production", "key", "microsoft.agent_framework")]
    [InlineData(LlmProviders.OpenRouter, "Testing", "", "stub")]
    public async Task ChatSpan_ShouldCarryProviderName(string configured, string environment, string apiKey, string expected)
    {
        using var capture = new SpanCapture();
        var loop = new AgentLoop(
            ScriptedLlmService.Replying(),
            new ToolRegistry([]),
            new AllowAllContentGuard(),
            Options.Create(new GuardrailOptions()),
            NullLogger<AgentLoop>.Instance,
            Options.Create(new LlmOptions { Provider = configured, ApiKey = apiKey, Model = "m" }),
            new FixedEnvironment(environment));

        await loop.RunAsync("go", "sys", null, []);

        var span = Assert.Single(capture.Spans(AiTelemetry.Chat));
        Assert.Equal(expected, span.GetTagItem("gen_ai.provider.name"));
        Assert.Equal("m", span.GetTagItem("gen_ai.request.model"));
    }

    [Fact]
    public async Task ExecuteToolSpan_ShouldSetErrorType_WhenToolDeniesPermission()
    {
        using var capture = new SpanCapture();

        var result = await Loop(CallsToolOnce("t"), new LambdaTool("t", (_, _) => throw new UnauthorizedAccessException("no")))
            .RunAsync("go", "sys", null, ["t"]);

        var span = Assert.Single(capture.Spans(AiTelemetry.ExecuteTool));
        Assert.Equal("UnauthorizedAccessException", span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("done", result.Reply);
    }

    [Fact]
    public async Task InvokeAgentSpan_ShouldSetErrorType_ForEveryWayTheChatFails()
    {
        using var capture = new SpanCapture();
        var provider = TestServiceFactory.CreateWithAi(nameof(InvokeAgentSpan_ShouldSetErrorType_ForEveryWayTheChatFails), services =>
        {
            services.AddSingleton<ILlmService>(ScriptedLlmService.Replying(input: 1, output: 1));
            services.Configure<AiQuotaOptions>(o => o.DailyTokensPerTenant = 2);
        });
        var other = await CreateAgentAsync(provider, "Other", null);
        var first = await ChatAsync(provider, new ChatAiCommand("spends the quota"));

        await Assert.ThrowsAsync<NotFoundException>(() => ChatAsync(provider, new ChatAiCommand("hi", ConversationId: Guid.NewGuid())));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ChatAsync(provider, new ChatAiCommand("hi", AgentId: other, ConversationId: first.ConversationId)));
        await Assert.ThrowsAsync<TooManyRequestsException>(() => ChatAsync(provider, new ChatAiCommand("hi")));

        var blocked = TestServiceFactory.CreateWithAi($"{nameof(InvokeAgentSpan_ShouldSetErrorType_ForEveryWayTheChatFails)}-guard", services =>
        {
            services.AddSingleton<ILlmService>(ScriptedLlmService.Replying());
            services.AddSingleton<IContentGuard>(new BlockingContentGuard(GuardSubject.UserMessage));
        });
        await Assert.ThrowsAsync<ContentBlockedException>(() => ChatAsync(blocked, new ChatAiCommand("hi")));

        var failures = capture.Spans(AiTelemetry.InvokeAgent)
            .Where(s => s.Status == ActivityStatusCode.Error)
            .Select(s => s.GetTagItem("error.type") as string)
            .Order()
            .ToArray();
        Assert.Equal(
            new[] { "BusinessRuleException", "ContentBlockedException", "NotFoundException", "TooManyRequestsException" },
            failures);
    }

    [Fact]
    public async Task ActivitySource_ShouldHaveNoListeners_WhenTracesDisabled()
    {
        // Positive control first: the same switch, on, does register the provider and a listener.
        await using (var enabled = WithTraces(true))
        {
            Assert.NotNull(enabled.Services.GetService<TracerProvider>());
            Assert.True(AiTelemetry.Source.HasListeners());
        }

        await using var factory = WithTraces(false);
        using var client = await AiHttp.AdminClientAsync(factory);

        Assert.Null(factory.Services.GetService<TracerProvider>());
        Assert.False(AiTelemetry.Source.HasListeners());

        var response = await client.PostAsync("/api/v1/ai/chat", System.Net.Http.Json.JsonContent.Create(new { message = "hi" }));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.False(AiTelemetry.Source.HasListeners());
    }

    /// <summary>
    /// A host setting, not an in-memory one: <c>AddObservability</c> reads the flag while services
    /// are registered, before the factory's in-memory configuration exists.
    /// </summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithTraces(bool on) =>
        AiHttp.Factory().WithWebHostBuilder(builder => builder.UseSetting("OpenTelemetry:EnableTraces", on ? "true" : "false"));

    // ---- helpers ---------------------------------------------------------------------------

    private static IServiceProvider Provider(string name, ILlmService llm) =>
        TestServiceFactory.CreateWithAi(name, services => services.AddSingleton(llm));

    private static async Task<ChatAiOutput> ChatAsync(IServiceProvider provider, ChatAiCommand command)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        TestServiceFactory.SetUser(scope.ServiceProvider, TestServiceFactory.DefaultUserId);
        return await scope.ServiceProvider.GetRequiredService<ChatAiHandler>().Handle(command, CancellationToken.None);
    }

    private static async Task<Guid> CreateAgentAsync(IServiceProvider provider, string name, string? model)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        return (await scope.ServiceProvider.GetRequiredService<CreateAgentHandler>()
            .Handle(new CreateAgentCommand(name, "x", [], Model: model), CancellationToken.None)).AgentId;
    }

    private sealed class FixedEnvironment(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static async Task<Agent> DefaultAgentAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        return (await scope.ServiceProvider.GetRequiredService<IAgentRepository>().GetDefaultAsync())!;
    }

    private static AgentLoop Loop(ILlmService llm, ITool? tool = null) =>
        new(
            llm,
            new ToolRegistry(tool is null ? [] : [tool]),
            new AllowAllContentGuard(),
            Options.Create(new GuardrailOptions()),
            NullLogger<AgentLoop>.Instance);

    private static ScriptedLlmService CallsToolOnce(string name) =>
        new((request, _) => Task.FromResult(
            request.History?.Any(m => m.Role == "tool") == true
                ? new LlmResponse("done", 1)
                : new LlmResponse(string.Empty, 1, [new ToolCall("c1", name, [])])));
}

/// <summary>
/// Records the Ai source's spans under a root activity this capture owns, so spans from other
/// tests running at the same time (a listener sees the whole process) are filtered out by trace id.
/// </summary>
internal sealed class SpanCapture : IDisposable
{
    private static readonly ActivitySource TestSource = new("Api.Tests.Telemetry");

    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;
    private readonly Activity _root;

    public SpanCapture()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is AiTelemetry.ActivitySourceName or "Api.Tests.Telemetry",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _stopped.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_listener);
        _root = TestSource.StartActivity("test-root")!;
    }

    public List<Activity> Spans(string? operation = null) =>
        _stopped
            .Where(a => a.Source.Name == AiTelemetry.ActivitySourceName && a.TraceId == _root.TraceId)
            .Where(a => operation is null || a.GetTagItem(AiTelemetry.OperationName) as string == operation)
            .ToList();

    public void Dispose()
    {
        _root.Dispose();
        _listener.Dispose();
    }
}
