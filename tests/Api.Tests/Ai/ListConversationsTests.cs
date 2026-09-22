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
    public async Task List_ShouldReturnOnlyCallersConversations_OverHttp()
    {
        await using var factory = ConversationHttp.Factory();
        using var alice = await AiHttp.PlainUserClientAsync(factory);
        using var bob = await AiHttp.PlainUserClientAsync(factory);
        var alicesFirst = await ConversationHttp.StartConversationAsync(alice, "primeira");
        var alicesSecond = await ConversationHttp.StartConversationAsync(alice, "segunda");
        var bobs = await ConversationHttp.StartConversationAsync(bob);

        var page = await alice.GetFromJsonAsync<PaginatedListOutput<ConversationSummary>>("/api/v1/ai/conversations");

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(new[] { alicesSecond, alicesFirst }, page.Data.Select(c => c.ConversationId).ToArray());
        Assert.DoesNotContain(page.Data, c => c.ConversationId == bobs);
    }

    [Fact]
    public async Task Handle_ShouldFilterBySearchTerm_AndSortByTitleOrLastActivityAt()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldFilterBySearchTerm_AndSortByTitleOrLastActivityAt));
        var now = DateTime.UtcNow;
        var banana = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, now.AddHours(-3), "banana split", ("user", "a"));
        var apple = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, now.AddHours(-1), "apple pie", ("user", "a"));
        var cherry = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, now.AddHours(-2), "cherry pie", ("user", "a"));

        Task<PaginatedListOutput<ConversationSummary>> List(ListConversationsQuery query) =>
            ConversationHandlers.RunAsync(provider, TestServiceFactory.DefaultUserId, sp =>
                sp.GetRequiredService<ListConversationsHandler>().Handle(query, CancellationToken.None));

        Assert.Equal(new[] { apple.Id, cherry.Id }, (await List(new ListConversationsQuery(SearchTerm: "pie"))).Data.Select(c => c.ConversationId).ToArray());
        Assert.Equal(new[] { apple.Id, banana.Id, cherry.Id }, (await List(new ListConversationsQuery(SortBy: "title"))).Data.Select(c => c.ConversationId).ToArray());
        Assert.Equal(new[] { cherry.Id, banana.Id, apple.Id }, (await List(new ListConversationsQuery(SortBy: "title", SortDirection: "desc"))).Data.Select(c => c.ConversationId).ToArray());
        Assert.Equal(new[] { banana.Id, cherry.Id, apple.Id }, (await List(new ListConversationsQuery(SortBy: "lastActivityAt", SortDirection: "asc"))).Data.Select(c => c.ConversationId).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task List_ShouldReturn400_WhenPageSizeIsOutOfRange(int pageSize)
    {
        await using var factory = ConversationHttp.Factory();
        using var client = await AiHttp.PlainUserClientAsync(factory);

        var response = await client.GetAsync($"/api/v1/ai/conversations?pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
