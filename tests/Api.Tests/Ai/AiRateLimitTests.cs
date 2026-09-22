using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

public sealed class AiRateLimitTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;
    private static readonly string[] TwoModels = [StubModelCatalog.ModelA, StubModelCatalog.ModelB];

    // ---- rate limit ------------------------------------------------------------------------

    [Fact]
    public async Task Chat_ShouldReturn429_WhenTenantExceedsRateLimit()
    {
        var llm = ScriptedLlmService.Replying();
        await using var factory = Factory(llm, settings => settings["Ai:RateLimit:PermitLimit"] = "2");
        using var client = await AiHttp.AdminClientAsync(factory);

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests }, statuses);
        var problem = await last!.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("AI rate limit exceeded", problem!.Title);
        Assert.Equal("Limite de pedidos de IA do tenant atingido. Tente novamente dentro de instantes.", problem.Detail);
        Assert.Equal(2, llm.Requests.Count);
    }

    [Fact]
    public async Task ChatAndComparisons_ShouldShareOneBucket()
    {
        await using var factory = Factory(ScriptedLlmService.Replying(), settings => settings["Ai:RateLimit:PermitLimit"] = "2");
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var chat = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });
        var first = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new { agentId = agent.AgentId, prompt = "hello", models = TwoModels });
        var second = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new { agentId = agent.AgentId, prompt = "hello", models = TwoModels });

        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public void Policy_ShouldPartitionByTenant()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, TenantContext>();
        using var root = services.BuildServiceProvider();
        var policy = new AiRateLimitPolicy(Options.Create(new AiRateLimitOptions()));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        string KeyFor(Guid? tenantId)
        {
            var scope = root.CreateScope();
            if (tenantId is { } id)
                ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).SetTenant(id, "k");
            return policy.GetPartition(new DefaultHttpContext { RequestServices = scope.ServiceProvider }).PartitionKey;
        }

        Assert.Equal(tenantA.ToString("N"), KeyFor(tenantA));
        Assert.Equal(KeyFor(tenantA), KeyFor(tenantA));
        Assert.NotEqual(KeyFor(tenantA), KeyFor(tenantB));
        Assert.Equal("none", KeyFor(null));
    }

    [Fact]
    public async Task AuthPolicy_ShouldStillReturn429WithEmptyBody_AfterPipelineReorder()
    {
        // Testing environment: the `auth` policy allows 200 requests per minute.
        await using var factory = AiHttp.Factory();
        using var client = factory.CreateClient();

        for (var i = 0; i < 200; i++)
        {
            var allowed = await client.PostAsync("/api/v1/identity/refresh", content: null);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var rejected = await client.PostAsync("/api/v1/identity/refresh", content: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(string.Empty, await rejected.Content.ReadAsStringAsync());
    }

    // ---- quota -----------------------------------------------------------------------------

    [Fact]
    public async Task Chat_ShouldReturn429_WhenDailyTokenQuotaReached()
    {
        var llm = ScriptedLlmService.Replying(input: 1, output: 1);
        await using var factory = IsolatedLedgerFactory(llm, settings => settings["Ai:Quota:DailyTokensPerTenant"] = "2");
        using var client = await AiHttp.AdminClientAsync(factory);

        var first = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });
        var second = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("AI quota exceeded", problem!.Title);
        Assert.Equal("Limite diário de tokens de IA do tenant atingido.", problem.Detail);
        Assert.Single(llm.Requests);
    }

    [Fact]
    public async Task Comparisons_ShouldReturn429_WhenDailyTokenQuotaReached()
    {
        var llm = ScriptedLlmService.Replying(input: 1, output: 1);
        await using var factory = IsolatedLedgerFactory(llm, settings => settings["Ai:Quota:DailyTokensPerTenant"] = "2");
        using var client = await AiHttp.AdminClientAsync(factory);
        var agent = await AiHttp.CreateAgentAsync(client);

        var chat = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });
        var compare = await client.PostAsJsonAsync("/api/v1/ai/comparisons", new { agentId = agent.AgentId, prompt = "hello", models = TwoModels });

        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, compare.StatusCode);
        var problem = await compare.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("AI quota exceeded", problem!.Title);
        Assert.Equal("Limite diário de tokens de IA do tenant atingido.", problem.Detail);
        Assert.Single(llm.Requests);
    }

    [Fact]
    public async Task Quota_ShouldCountFromUtcMidnight_OfCurrentDay()
    {
        var ledger = new CountingUsageRepository();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 22, 23, 30, 0, TimeSpan.FromHours(-3)));
        var quota = new AiQuota(ledger, Options.Create(new AiQuotaOptions { DailyTokensPerTenant = 10 }), clock);

        await quota.EnsureWithinAsync(CancellationToken.None);

        // 23:30 at UTC-3 is 02:30 UTC on the 23rd: the window opens at 2026-09-23T00:00Z.
        Assert.Equal(new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc), ledger.LastSince);
    }

    [Fact]
    public async Task Quota_ShouldThrowAtLimit_AndPassBelowIt()
    {
        var options = Options.Create(new AiQuotaOptions { DailyTokensPerTenant = 10 });
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        await new AiQuota(new CountingUsageRepository(spent: 9), options, clock).EnsureWithinAsync(CancellationToken.None);

        var error = await Assert.ThrowsAsync<TooManyRequestsException>(() =>
            new AiQuota(new CountingUsageRepository(spent: 10), options, clock).EnsureWithinAsync(CancellationToken.None));
        Assert.Equal("AI quota exceeded", error.Title);
        Assert.Equal("Limite diário de tokens de IA do tenant atingido.", error.Message);
    }

    [Fact]
    public async Task UsageRepository_ShouldSumTokens_SinceInstant_ForCurrentTenant()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(UsageRepository_ShouldSumTokens_SinceInstant_ForCurrentTenant));
        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(AiUsageEntry.From(Record(TenantId, input: 3, output: 4)));
            db.Add(AiUsageEntry.From(Record(Guid.NewGuid(), input: 100, output: 100)));
            await db.SaveChangesAsync();
        }

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var repository = scope.ServiceProvider.GetRequiredService<IAiUsageRepository>();

        Assert.Equal(7, await repository.SumTokensSinceAsync(DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(0, await repository.SumTokensSinceAsync(DateTime.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public async Task Handle_ShouldNotQueryLedger_WhenQuotaIsZero()
    {
        Assert.Equal(0, await LedgerQueriesForOneChatAsync(quota: 0));
        Assert.Equal(1, await LedgerQueriesForOneChatAsync(quota: 1_000));
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static WebApplicationFactory<Program> Factory(ILlmService llm, Action<Dictionary<string, string?>> configure) =>
        AiHttp.Factory(configure).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(llm)));

    /// <summary>
    /// The host's InMemory store is named `AppDb` process-wide, so every HTTP test in the run
    /// writes usage into the same `dev` ledger. A quota proof needs a ledger only it has written.
    /// </summary>
    private static WebApplicationFactory<Program> IsolatedLedgerFactory(ILlmService llm, Action<Dictionary<string, string?>> configure)
    {
        var databaseName = $"quota-{Guid.NewGuid():N}";
        return AiHttp.Factory(configure).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(llm);
                services.ConfigureDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
    }

    private static async Task<int> LedgerQueriesForOneChatAsync(long quota)
    {
        var counting = new CountingUsageRepository();
        var provider = TestServiceFactory.CreateWithAi(
            $"{nameof(Handle_ShouldNotQueryLedger_WhenQuotaIsZero)}-{quota}",
            services =>
            {
                services.AddSingleton<ILlmService>(ScriptedLlmService.Replying());
                services.AddSingleton<IAiUsageRepository>(counting);
                services.Configure<AiQuotaOptions>(options => options.DailyTokensPerTenant = quota);
            });

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var result = await scope.ServiceProvider.GetRequiredService<ChatAiHandler>()
            .Handle(new ChatAiCommand("hello"), CancellationToken.None);

        Assert.Equal("ok", result.Reply);
        return counting.SumCalls;
    }

    private static AiUsageRecord Record(Guid tenantId, int input, int output) =>
        new("llm", "stub", "m", "ai", AiUsageOperations.Chat, tenantId, Guid.NewGuid(), input, output, null,
            TimeSpan.FromMilliseconds(1), true, null);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private sealed class CountingUsageRepository(long spent = 0) : IAiUsageRepository
    {
        public int SumCalls { get; private set; }
        public DateTime? LastSince { get; private set; }

        public Task AddAsync(AiUsageEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AiUsageEntry>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiUsageEntry>>([]);

        public Task<long> SumTokensSinceAsync(DateTime since, CancellationToken cancellationToken = default)
        {
            SumCalls++;
            LastSince = since;
            return Task.FromResult(spent);
        }
    }
}
