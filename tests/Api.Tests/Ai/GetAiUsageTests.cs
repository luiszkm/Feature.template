using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class GetAiUsageTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;
    private static readonly DateTime Day = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ShouldReturnAggregatedRows_PerAgent()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldReturnAggregatedRows_PerAgent));
        var (alpha, beta) = await TwoAgentsAsync(provider);
        await SeedAsync(provider,
            Row(alpha, 10, 5, true, Day),
            Row(alpha, 3, 1, false, Day.AddHours(1)),
            Row(beta, 1, 1, true, Day.AddHours(-1)),
            Row(alpha, 999, 999, true, Day, tenantId: Guid.NewGuid()));

        var page = await UsageAsync(provider, new GetAiUsageQuery());

        var a = page.Data.Single(r => r.AgentId == alpha);
        Assert.Equal(("Alpha", 2, 1, 13L, 6L, 19L, Day.AddHours(1)), (a.AgentName, a.Calls, a.Failures, a.InputTokens, a.OutputTokens, a.TotalTokens, a.LastUsedAt));
        var b = page.Data.Single(r => r.AgentId == beta);
        Assert.Equal(("Beta", 1, 0, 1L, 1L, 2L, Day.AddHours(-1)), (b.AgentName, b.Calls, b.Failures, b.InputTokens, b.OutputTokens, b.TotalTokens, b.LastUsedAt));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task Handle_ShouldSortByTotalTokensDesc_StableByAgentId_WhenNoParamsGiven()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldSortByTotalTokensDesc_StableByAgentId_WhenNoParamsGiven));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        await SeedAsync(provider,
            Row(ids[0], 1, 1, true, Day),
            Row(ids[1], 5, 5, true, Day),
            Row(ids[2], 1, 1, true, Day));

        var page = await UsageAsync(provider, new GetAiUsageQuery());

        var ties = new[] { ids[0], ids[2] }.OrderBy(id => id).ToArray();
        Assert.Equal(new[] { ids[1], ties[0], ties[1] }, page.Data.Select(r => r.AgentId).ToArray());
        Assert.Equal((1, 20), (page.PageNumber, page.PageSize));
    }

    [Fact]
    public async Task Handle_ShouldFilterByCreatedAt_WithinInclusiveRange()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldFilterByCreatedAt_WithinInclusiveRange));
        var agent = Guid.NewGuid();
        await SeedAsync(provider,
            Row(agent, 1, 0, true, Day.AddTicks(-1)),
            Row(agent, 10, 0, true, Day),
            Row(agent, 100, 0, true, Day.AddHours(1)),
            Row(agent, 1000, 0, true, Day.AddHours(1).AddTicks(1)));

        var page = await UsageAsync(provider, new GetAiUsageQuery(From: Day, To: Day.AddHours(1)));

        var row = Assert.Single(page.Data);
        Assert.Equal((2, 110L), (row.Calls, row.InputTokens));
    }

    [Fact]
    public void Validator_ShouldFail_WhenFromIsAfterTo()
    {
        var validator = new GetAiUsageValidator();

        validator.TestValidate(new GetAiUsageQuery(From: Day.AddTicks(1), To: Day)).ShouldHaveValidationErrorFor(x => x.From);
        validator.TestValidate(new GetAiUsageQuery(From: Day, To: Day)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Get_ShouldReturn200_WithAggregatedRowShape()
    {
        await using var factory = WithLlm();
        using var client = await AiHttp.AdminClientAsync(factory);
        await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        var response = await client.GetAsync("/api/v1/ai/usage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = json.RootElement.GetProperty("data")[0];
        Assert.Equal(
            new[] { "agentId", "agentName", "calls", "failures", "inputTokens", "lastUsedAt", "outputTokens", "totalTokens" },
            first.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task Get_ShouldReturn400_WhenFromIsAfterTo()
    {
        await using var factory = WithLlm();
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/usage?from=2026-09-23T00:00:00Z&to=2026-09-22T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenCallerLacksReadPermission()
    {
        await using var factory = WithLlm();
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/usage");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = AiHttp.Factory(settings => settings["FeatureFlags:EnableAI"] = "false");
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/usage");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Feature disabled", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = WithLlm();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await client.GetAsync("/api/v1/ai/usage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static WebApplicationFactory<Program> WithLlm() =>
        AiHttp.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying())));

    private static async Task<(Guid Alpha, Guid Beta)> TwoAgentsAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var alpha = await create.Handle(new CreateAgentCommand("Alpha", "x", []), CancellationToken.None);
        var beta = await create.Handle(new CreateAgentCommand("Beta", "x", []), CancellationToken.None);
        return (alpha.AgentId, beta.AgentId);
    }

    private static AiUsageEntry Row(Guid agentId, int input, int output, bool success, DateTime createdAt, Guid? tenantId = null)
    {
        var entry = AiUsageEntry.From(new AiUsageRecord(
            "llm", "stub", "stub", "ai", AiUsageOperations.Chat, tenantId ?? TenantId, agentId,
            input, output, null, TimeSpan.FromMilliseconds(1), success, success ? null : "HttpRequestException"));
        entry.CreatedAt = createdAt;
        return entry;
    }

    private static async Task SeedAsync(IServiceProvider provider, params AiUsageEntry[] rows)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<PaginatedListOutput<AiUsageOutput>> UsageAsync(IServiceProvider provider, GetAiUsageQuery query)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        return await scope.ServiceProvider.GetRequiredService<GetAiUsageHandler>().Handle(query, CancellationToken.None);
    }
}
