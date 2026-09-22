using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Ai;

public sealed class AiUsageTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Chat_ShouldPersistOneUsageEntry_OnSuccess()
    {
        var llm = ScriptedLlmService.Replying(input: 3, output: 2, cost: 0.004m);
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Chat_ShouldPersistOneUsageEntry_OnSuccess),
            services => services.AddSingleton<ILlmService>(llm));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var agent = await CreateAgentAsync(scope.ServiceProvider, model: null);

        await scope.ServiceProvider.GetRequiredService<ChatAiHandler>()
            .Handle(new ChatAiCommand("hello", AgentId: agent.AgentId), CancellationToken.None);

        var entry = Assert.Single(await ListEntriesAsync(provider));
        Assert.Equal(agent.AgentId, entry.AgentId);
        Assert.Equal(AiUsageOperations.Chat, entry.Operation);
        Assert.Equal("stub", entry.Model); // Ai:Llm:Model in TestServiceFactory, the effective model
        Assert.Equal(3, entry.InputTokens);
        Assert.Equal(2, entry.OutputTokens);
        Assert.Equal(0.004m, entry.Cost);
        Assert.True(entry.Success);
        Assert.Null(entry.ErrorCode);
    }

    [Fact]
    public async Task Chat_ShouldRecordAgentModel_WhenAgentHasModel()
    {
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Chat_ShouldRecordAgentModel_WhenAgentHasModel),
            services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying()));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var agent = await CreateAgentAsync(scope.ServiceProvider, model: StubModelCatalog.ModelB);

        await scope.ServiceProvider.GetRequiredService<ChatAiHandler>()
            .Handle(new ChatAiCommand("hello", AgentId: agent.AgentId), CancellationToken.None);

        Assert.Equal(StubModelCatalog.ModelB, Assert.Single(await ListEntriesAsync(provider)).Model);
    }

    [Fact]
    public async Task Chat_ShouldPersistFailedEntry_WhenLoopThrows()
    {
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Chat_ShouldPersistFailedEntry_WhenLoopThrows),
            services => services.AddSingleton<ILlmService>(new ScriptedLlmService((_, _) =>
                throw new HttpRequestException("upstream"))));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            chat.Handle(new ChatAiCommand("hello"), CancellationToken.None));

        var entry = Assert.Single(await ListEntriesAsync(provider));
        Assert.False(entry.Success);
        Assert.Equal(nameof(HttpRequestException), entry.ErrorCode);
        Assert.Equal(0, entry.InputTokens);
        Assert.Equal(0, entry.OutputTokens);
        Assert.Null(entry.Cost);
    }

    [Fact]
    public async Task Chat_ShouldRethrowOriginalException_WhenLoopThrows()
    {
        var original = new InvalidOperationException("boom");
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Chat_ShouldRethrowOriginalException_WhenLoopThrows),
            services => services.AddSingleton<ILlmService>(new ScriptedLlmService((_, _) => throw original)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var chat = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            chat.Handle(new ChatAiCommand("hello"), CancellationToken.None));

        Assert.Same(original, thrown);
    }

    [Fact]
    public async Task Tracker_ShouldLogAndSwallow_WhenSaveFails()
    {
        var logs = new ListLoggerProvider();
        var provider = TestServiceFactory.CreateWithAi(
            nameof(Tracker_ShouldLogAndSwallow_WhenSaveFails),
            services =>
            {
                services.AddSingleton<ILlmService>(ScriptedLlmService.Replying("fine"));
                services.AddSingleton<ILoggerProvider>(logs);
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new FailingSaveInterceptor()));
            });

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var agent = await CreateAgentAsync(scope.ServiceProvider, model: null);
        FailingSaveInterceptor.Armed.Value = true;
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<ChatAiHandler>()
                .Handle(new ChatAiCommand("hello", AgentId: agent.AgentId), CancellationToken.None);

            Assert.Equal("fine", result.Reply);
        }
        finally
        {
            FailingSaveInterceptor.Armed.Value = false;
        }

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Failed to persist AI usage"));
        Assert.Empty(await ListEntriesAsync(provider));
    }

    [Fact]
    public async Task Chat_ShouldRecordStubProvider_InTesting()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Chat_ShouldRecordStubProvider_InTesting));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        await scope.ServiceProvider.GetRequiredService<ChatAiHandler>()
            .Handle(new ChatAiCommand("hello"), CancellationToken.None);

        Assert.Equal("stub", Assert.Single(await ListEntriesAsync(provider)).Provider);
    }

    [Fact]
    public void UsageEntry_ShouldHoldOnlyMetadataProperties()
    {
        var properties = typeof(AiUsageEntry).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(
            new[]
            {
                "AgentId", "Cost", "CreatedAt", "ErrorCode", "Id", "InputTokens", "LatencyMs", "Model",
                "Module", "Operation", "OutputTokens", "Provider", "Success", "TenantId"
            },
            properties);
    }

    [Fact]
    public void UsageRepository_ShouldExposeNoUpdateOrDelete()
    {
        var methods = typeof(IAiUsageRepository).GetMethods().Select(m => m.Name).Order().ToArray();

        Assert.Equal(new[] { "AddAsync", "ListAsync", "SumTokensSinceAsync" }, methods);
    }

    [Fact]
    public async Task UsageEntries_ShouldBeTenantFiltered()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(UsageEntries_ShouldBeTenantFiltered));

        using (var scope = provider.CreateScope())
        {
            TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
            await scope.ServiceProvider.GetRequiredService<ChatAiHandler>()
                .Handle(new ChatAiCommand("hello"), CancellationToken.None);
        }

        Assert.Single(await ListEntriesAsync(provider, TenantId));
        Assert.Empty(await ListEntriesAsync(provider, Guid.NewGuid()));
    }

    private static Task<AgentOutput> CreateAgentAsync(IServiceProvider services, string? model) =>
        services.GetRequiredService<CreateAgentHandler>().Handle(
            new CreateAgentCommand($"Usage {Guid.NewGuid():N}", "x", [], Model: model),
            CancellationToken.None);

    private static async Task<IReadOnlyList<AiUsageEntry>> ListEntriesAsync(IServiceProvider provider, Guid? tenantId = null)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, tenantId ?? TenantId);
        return await scope.ServiceProvider.GetRequiredService<IAiUsageRepository>().ListAsync();
    }
}

/// <summary>Registered on every test AppDbContext; throws on save only while armed for the current async flow.</summary>
internal sealed class FailingSaveInterceptor : SaveChangesInterceptor
{
    public static readonly AsyncLocal<bool> Armed = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Armed.Value && eventData.Context?.ChangeTracker.Entries<AiUsageEntry>().Any() == true)
            throw new DbUpdateException("Simulated usage write failure.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

internal sealed class ListLoggerProvider : ILoggerProvider
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new ListLogger(this);

    public void Dispose()
    {
    }

    private sealed class ListLogger(ListLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (owner.Entries)
                owner.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
