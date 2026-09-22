using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class GetConversationTests
{
    private static readonly (string, string)[] Transcript =
    [
        ("user", "q"), ("assistant", ""), ("tool", "<tool_output>\n{\"n\":1}\n</tool_output>"), ("assistant", "a")
    ];

    [Fact]
    public async Task Handle_ShouldReturnUserAndAssistantItems_OrderedBySequence()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldReturnUserAndAssistantItems_OrderedBySequence));
        var seeded = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, DateTime.UtcNow, "t", Transcript);

        var output = await Get(provider, TestServiceFactory.DefaultUserId, seeded.Id);

        Assert.Equal(new[] { (1, "user"), (2, "assistant"), (4, "assistant") }, output.Items.Select(i => (i.Sequence, i.Role)).ToArray());
    }

    [Fact]
    public async Task Handle_ShouldIncludeToolItems_WhenIncludeToolItemsIsTrue()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldIncludeToolItems_WhenIncludeToolItemsIsTrue));
        var seeded = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, DateTime.UtcNow, "t", Transcript);

        var output = await Get(provider, TestServiceFactory.DefaultUserId, seeded.Id, includeToolItems: true);

        Assert.Equal(new[] { 1, 2, 3, 4 }, output.Items.Select(i => i.Sequence).ToArray());
        Assert.Equal("<tool_output>\n{\"n\":1}\n</tool_output>", output.Items.Single(i => i.Role == "tool").Content);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenConversationBelongsToAnotherUser()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldThrow_WhenConversationBelongsToAnotherUser));
        var foreign = await ConversationTestSupport.SeedAsync(provider, ConversationTestSupport.OtherUserId, DateTime.UtcNow, "t", ("user", "x"));

        await Assert.ThrowsAsync<NotFoundException>(() => Get(provider, TestServiceFactory.DefaultUserId, foreign.Id));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCallerIsAdminButNotOwner()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldThrow_WhenCallerIsAdminButNotOwner));
        var foreign = await ConversationTestSupport.SeedAsync(provider, ConversationTestSupport.OtherUserId, DateTime.UtcNow, "t", ("user", "x"));

        await Assert.ThrowsAsync<NotFoundException>(() => Get(provider, TestServiceFactory.DefaultUserId, foreign.Id, roles: "Admin"));
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = ConversationHttp.Factory(enableAi: false);
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/v1/ai/conversations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Feature disabled", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WithoutAiAgentReadOrManagePermission()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = await AiHttp.PlainUserClientAsync(factory);
        var conversationId = await ConversationHttp.StartConversationAsync(client, "pergunta");

        var response = await client.GetAsync($"/api/v1/ai/conversations/{conversationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = await response.Content.ReadFromJsonAsync<ConversationOutput>();
        Assert.Equal(new[] { ("user", "pergunta"), ("assistant", "reply") }, output!.Items.Select(i => (i.Role, i.Content)).ToArray());
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = ConversationHttp.Anonymous(factory);

        var response = await client.GetAsync($"/api/v1/ai/conversations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<ConversationOutput> Get(IServiceProvider provider, Guid userId, Guid conversationId, bool includeToolItems = false, params string[] roles) =>
        ConversationHandlers.RunAsync(provider, userId, sp =>
            sp.GetRequiredService<GetConversationHandler>().Handle(new GetConversationQuery(conversationId, includeToolItems), CancellationToken.None), roles);
}
