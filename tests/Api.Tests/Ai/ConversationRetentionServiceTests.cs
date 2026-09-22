using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

public sealed class ConversationRetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PurgeAsync_ShouldDeleteConversationsOlderThanRetentionDays_WithTheirItems()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(PurgeAsync_ShouldDeleteConversationsOlderThanRetentionDays_WithTheirItems));
        var old = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, Now.UtcDateTime.AddDays(-100), "old", ("user", "a"), ("assistant", "b"));
        var edge = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, Now.UtcDateTime.AddDays(-89), "edge", ("user", "a"));
        var fresh = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, Now.UtcDateTime, "fresh", ("user", "a"));

        var deleted = await Service(provider, retentionDays: 90).PurgeAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(
            new[] { edge.Id, fresh.Id }.Order(),
            (await ConversationTestSupport.AllConversationsAsync(provider)).Select(c => c.Id).Order());
        Assert.Empty(await ConversationTestSupport.ItemsAsync(provider, old.Id));
    }

    [Fact]
    public async Task PurgeAsync_ShouldNotDeleteAnyConversation_WhenRetentionDaysIsZero()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(PurgeAsync_ShouldNotDeleteAnyConversation_WhenRetentionDaysIsZero));
        await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, Now.UtcDateTime.AddDays(-1000), "ancient", ("user", "a"));
        await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, Now.UtcDateTime, "fresh", ("user", "a"));

        var deleted = await Service(provider, retentionDays: 0).PurgeAsync(CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal(2, (await ConversationTestSupport.AllConversationsAsync(provider)).Count);
    }

    [Fact]
    public async Task PurgeAsync_ShouldIgnoreQueryFiltersAcrossTenants_AndLogDeletedCount()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(PurgeAsync_ShouldIgnoreQueryFiltersAcrossTenants_AndLogDeletedCount));
        await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, Now.UtcDateTime.AddDays(-100), "dev", ("user", "a"));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var agentId = (await db.Set<Agent>().IgnoreQueryFilters().FirstAsync()).Id;
            var other = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), agentId, "other tenant", Now.UtcDateTime.AddDays(-100));
            other.Append(ConversationRoles.User, "x", Now.UtcDateTime.AddDays(-100));
            db.Add(other);
            await db.SaveChangesAsync();
        }
        var logger = new ListLogger<ConversationRetentionService>();

        var deleted = await Service(provider, retentionDays: 90, logger: logger).PurgeAsync(CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Empty(await ConversationTestSupport.AllConversationsAsync(provider));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("deleted 2 conversations"));
    }

    [Fact]
    public async Task PurgeAsync_ShouldLogAndContinueScheduling_WhenAPurgePassThrows()
    {
        var scopes = new ThrowingScopeFactory();
        var logger = new ListLogger<ConversationRetentionService>();
        var service = new ConversationRetentionService(
            scopes,
            Options.Create(new ConversationOptions { RetentionDays = 90, PurgeIntervalHours = 24 }),
            new ImmediateTimeProvider(),
            logger);

        await service.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (scopes.Calls < 3 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        await service.StopAsync(CancellationToken.None);

        Assert.True(scopes.Calls >= 3, $"expected at least 3 passes, saw {scopes.Calls}");
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message == "Conversation retention pass failed");
        Assert.Equal(TimeSpan.FromHours(24), ImmediateTimeProvider.LastDueTime);
    }

    [Fact]
    public async Task Options_ShouldBindDefaults_FromAppsettingsJson()
    {
        await using var factory = TestWebApplicationFactory.Create();

        var options = factory.Services.GetRequiredService<IOptions<ConversationOptions>>().Value;

        Assert.Equal(20, options.HistoryWindow);
        Assert.Equal(200, options.MaxItems);
        Assert.Equal(90, options.RetentionDays);
        Assert.Equal(24, options.PurgeIntervalHours);

        var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(AppsettingsPath()));
        var block = json.RootElement.GetProperty("Ai").GetProperty("Conversations");
        Assert.Equal(20, block.GetProperty("HistoryWindow").GetInt32());
        Assert.Equal(200, block.GetProperty("MaxItems").GetInt32());
        Assert.Equal(90, block.GetProperty("RetentionDays").GetInt32());
        Assert.Equal(24, block.GetProperty("PurgeIntervalHours").GetInt32());
    }

    private static ConversationRetentionService Service(
        IServiceProvider provider,
        int retentionDays,
        ILogger<ConversationRetentionService>? logger = null) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ConversationOptions { RetentionDays = retentionDays }),
            new FixedTimeProvider(Now),
            logger ?? new ListLogger<ConversationRetentionService>());

    private static string AppsettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "features.json")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "Api", "appsettings.json");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Every timer fires at once, so the service's delay between passes elapses immediately.</summary>
    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public static TimeSpan LastDueTime;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            LastDueTime = dueTime;
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        private int _calls;

        public int Calls => _calls;

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _calls);
            throw new InvalidOperationException("database unavailable");
        }
    }
}
