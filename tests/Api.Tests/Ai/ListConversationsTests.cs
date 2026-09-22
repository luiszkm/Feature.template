using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class ListConversationsTests
{
    [Fact]
    public async Task Handle_ShouldReturnOnlyCallersConversations_OrderedByLastActivityAtDesc()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldReturnOnlyCallersConversations_OrderedByLastActivityAtDesc));
        var now = DateTime.UtcNow;
        var older = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, now.AddHours(-2), "older", ("user", "a"));
        var newer = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, now.AddHours(-1), "newer", ("user", "a"), ("assistant", "b"));
        await ConversationTestSupport.SeedAsync(provider, ConversationTestSupport.OtherUserId, now, "theirs", ("user", "x"));

        var page = await ConversationHandlers.RunAsync(provider, TestServiceFactory.DefaultUserId, sp =>
            sp.GetRequiredService<ListConversationsHandler>().Handle(new ListConversationsQuery(), CancellationToken.None));

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { newer.Id, older.Id }, page.Data.Select(c => c.ConversationId).ToArray());
        Assert.Equal(new[] { 2, 1 }, page.Data.Select(c => c.ItemCount).ToArray());
        Assert.Equal("newer", page.Data[0].Title);
    }

    [Fact]
    public async Task List_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = ConversationHttp.Factory(enableAi: false);
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/conversations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Feature disabled", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task List_ShouldReturn200_WithoutAiAgentReadOrManagePermission()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = await AiHttp.PlainUserClientAsync(factory);
        var conversationId = await ConversationHttp.StartConversationAsync(client);

        var response = await client.GetAsync("/api/v1/ai/conversations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PaginatedListOutput<ConversationSummary>>();
        Assert.Equal(conversationId, Assert.Single(page!.Data).ConversationId);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = ConversationHttp.Anonymous(factory);

        var response = await client.GetAsync("/api/v1/ai/conversations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
