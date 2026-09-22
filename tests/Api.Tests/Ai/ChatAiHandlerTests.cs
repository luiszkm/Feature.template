using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class ChatAiHandlerTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public void Validator_ShouldFail_WhenMessageIsEmpty()
    {
        var validator = new ChatAiValidator();
        var result = validator.TestValidate(new ChatAiCommand(""));
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public async Task Handle_ShouldReturnReply_WhenMessageIsValid()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldReturnReply_WhenMessageIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        var result = await handler.Handle(new ChatAiCommand("hello"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Reply));
        Assert.True(result.IterationsUsed > 0);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantIsMissing()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenTenantIsMissing));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new ChatAiCommand("hello"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUseAgentAllowlist_WhenAgentIdIsProvided()
    {
        var llm = new RecordingLlmService();
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Handle_ShouldUseAgentAllowlist_WhenAgentIdIsProvided),
            services => services.AddSingleton<ILlmService>(llm));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        var agent = await create.Handle(
            new CreateAgentCommand("Tenant only", "Use tenant tool.", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        var result = await chat.Handle(
            new ChatAiCommand("tell me about the tenant", AgentId: agent.AgentId),
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Reply));
        Assert.NotNull(llm.Last);
        Assert.Equal(agent.Instructions, llm.Last.SystemPrompt);
        Assert.Equal(
            new[] { AgentToolNames.GetTenantInfo },
            llm.Last.Tools?.Select(tool => tool.Name).ToArray());
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenAgentIdIsUnknown()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenAgentIdIsUnknown));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ChatAiCommand("hello", AgentId: Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenAgentIsInactive()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenAgentIsInactive));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var deactivate = scope.ServiceProvider.GetRequiredService<DeactivateAgentHandler>();
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        var extra = await create.Handle(
            new CreateAgentCommand("Parked", "x", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);
        await deactivate.Handle(new DeactivateAgentCommand(extra.AgentId), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            chat.Handle(new ChatAiCommand("hello", AgentId: extra.AgentId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldNotExecuteTool_OutsideAllowlist()
    {
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Handle_ShouldNotExecuteTool_OutsideAllowlist),
            services => services.AddScoped<IUserDirectory, ThrowingUserDirectory>());

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        var agent = await create.Handle(
            new CreateAgentCommand("No users", "x", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        var result = await chat.Handle(
            new ChatAiCommand("summarize users please", AgentId: agent.AgentId),
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Reply));
    }

    [Fact]
    public async Task Handle_ShouldAcceptHistory_WithoutPersistingThreads()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldAcceptHistory_WithoutPersistingThreads));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var history = new LlmMessage[] { new("user", "earlier") };
        await handler.Handle(new ChatAiCommand("hello", history), CancellationToken.None);

        Assert.Equal(0, db.Set<AgentFile>().Count());
    }

    [Fact]
    public async Task Handle_ShouldTrackConfiguredProviderAndModel()
    {
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Handle_ShouldTrackConfiguredProviderAndModel),
            services =>
            {
                services.AddSingleton<ILlmService, ImmediateLlmService>();
                services.AddSingleton<RecordingUsageTracker>();
                services.AddSingleton<IAiUsageTracker>(sp => sp.GetRequiredService<RecordingUsageTracker>());
            });

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmOptions>>();
        options.Value.ApiKey = "test-key";
        options.Value.Provider = LlmProviders.OpenRouter;
        options.Value.Model = "openai/gpt-4o-mini";

        var tracker = scope.ServiceProvider.GetRequiredService<RecordingUsageTracker>();
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        await handler.Handle(new ChatAiCommand("hello"), CancellationToken.None);

        var record = Assert.Single(tracker.Records);
        Assert.Equal(LlmProviders.OpenRouter, record.Provider);
        Assert.Equal("openai/gpt-4o-mini", record.Model);
    }

    [Fact]
    public async Task Handle_ShouldSendAgentModel_OnEveryLlmCall_IncludingSummary()
    {
        // Always asks for a tool: the loop runs its 5 iterations and then the summary call.
        var llm = new ScriptedLlmService((request, _) => Task.FromResult(
            request.Tools is { Count: > 0 }
                ? new LlmResponse(string.Empty, 1, [new ToolCall("c1", "unknown_tool", [])])
                : new LlmResponse("summary", 1)));
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Handle_ShouldSendAgentModel_OnEveryLlmCall_IncludingSummary),
            services => services.AddSingleton<ILlmService>(llm));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();
        var agent = await create.Handle(
            new CreateAgentCommand("Modelled", "x", [AgentToolNames.GetTenantInfo], Model: StubModelCatalog.ModelA),
            CancellationToken.None);

        var result = await chat.Handle(new ChatAiCommand("go", AgentId: agent.AgentId), CancellationToken.None);

        Assert.Equal("summary", result.Reply);
        Assert.Equal(6, llm.Requests.Count);
        Assert.All(llm.Requests, request => Assert.Equal(StubModelCatalog.ModelA, request.Model));
    }

    [Fact]
    public async Task Handle_ShouldSendNullModel_WhenAgentHasNoModel()
    {
        var llm = ScriptedLlmService.Replying();
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Handle_ShouldSendNullModel_WhenAgentHasNoModel),
            services => services.AddSingleton<ILlmService>(llm));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();
        var agent = await create.Handle(new CreateAgentCommand("Plain", "x", []), CancellationToken.None);

        await chat.Handle(new ChatAiCommand("go", AgentId: agent.AgentId), CancellationToken.None);

        Assert.All(llm.Requests, request => Assert.Null(request.Model));
    }

}

internal sealed class ThrowingUserDirectory : IUserDirectory
{
    public Task<PaginatedListOutput<UserDirectoryEntry>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("get_users_summary should not run.");
}

internal sealed class ImmediateLlmService : ILlmService
{
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LlmResponse("ok", 1));
}

internal sealed class RecordingLlmService : ILlmService
{
    public LlmRequest? Last { get; private set; }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        Last = request;
        return Task.FromResult(new LlmResponse("ok", 1));
    }
}

internal sealed class RecordingUsageTracker : IAiUsageTracker
{
    public List<AiUsageRecord> Records { get; } = [];

    public Task TrackAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }
}
