using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class DeleteConversationTests
{
    [Fact]
    public async Task Handle_ShouldRemoveConversationAndAllItems_WhenCallerIsOwner()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldRemoveConversationAndAllItems_WhenCallerIsOwner));
        var mine = await ConversationTestSupport.SeedAsync(provider, TestServiceFactory.DefaultUserId, DateTime.UtcNow, "t", ("user", "a"), ("assistant", "b"));
        var theirs = await ConversationTestSupport.SeedAsync(provider, ConversationTestSupport.OtherUserId, DateTime.UtcNow, "t", ("user", "x"));

        await Delete(provider, TestServiceFactory.DefaultUserId, mine.Id);

        Assert.Equal(new[] { theirs.Id }, (await ConversationTestSupport.AllConversationsAsync(provider)).Select(c => c.Id).ToArray());
        Assert.Empty(await ConversationTestSupport.ItemsAsync(provider, mine.Id));
        Assert.Single(await ConversationTestSupport.ItemsAsync(provider, theirs.Id));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenConversationBelongsToAnotherUser()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldThrow_WhenConversationBelongsToAnotherUser));
        var foreign = await ConversationTestSupport.SeedAsync(provider, ConversationTestSupport.OtherUserId, DateTime.UtcNow, "t", ("user", "x"));

        await Assert.ThrowsAsync<NotFoundException>(() => Delete(provider, TestServiceFactory.DefaultUserId, foreign.Id));

        Assert.Single(await ConversationTestSupport.AllConversationsAsync(provider));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCallerIsAdminButNotOwner()
    {
        var provider = ConversationHandlers.Provider(nameof(Handle_ShouldThrow_WhenCallerIsAdminButNotOwner));
        var foreign = await ConversationTestSupport.SeedAsync(provider, ConversationTestSupport.OtherUserId, DateTime.UtcNow, "t", ("user", "x"));

        await Assert.ThrowsAsync<NotFoundException>(() => Delete(provider, TestServiceFactory.DefaultUserId, foreign.Id, "Admin"));

        Assert.Single(await ConversationTestSupport.AllConversationsAsync(provider));
    }

    [Fact]
    public async Task Get_ShouldReturn404_AfterDelete()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = await AiHttp.AdminClientAsync(factory);
        var conversationId = await ConversationHttp.StartConversationAsync(client);

        var deleted = await client.DeleteAsync($"/api/v1/ai/conversations/{conversationId}");
        var read = await client.GetAsync($"/api/v1/ai/conversations/{conversationId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("Not found", (await read.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Delete_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = ConversationHttp.Factory(enableAi: false);
        using var client = await AiHttp.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/v1/ai/conversations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Feature disabled", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Delete_ShouldReturn204_WithoutAiAgentReadOrManagePermission()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = await AiHttp.PlainUserClientAsync(factory);
        var conversationId = await ConversationHttp.StartConversationAsync(client);

        var response = await client.DeleteAsync($"/api/v1/ai/conversations/{conversationId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = ConversationHttp.Factory();
        using var client = ConversationHttp.Anonymous(factory);

        var response = await client.DeleteAsync($"/api/v1/ai/conversations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<bool> Delete(IServiceProvider provider, Guid userId, Guid conversationId, params string[] roles) =>
        ConversationHandlers.RunAsync(provider, userId, sp =>
            sp.GetRequiredService<DeleteConversationHandler>().Handle(new DeleteConversationCommand(conversationId), CancellationToken.None), roles);
}
