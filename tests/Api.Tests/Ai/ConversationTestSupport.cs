using Api.Features.Ai;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

/// <summary>Seeds and reads conversations straight through the <c>AppDbContext</c>, past the owner filter.</summary>
internal static class ConversationTestSupport
{
    public static readonly Guid OtherUserId = Guid.Parse("0e9d6b1f-5a2c-4f38-8d7e-9c4b2a1f6e03");

    public static async Task<Conversation> SeedAsync(
        IServiceProvider provider,
        Guid userId,
        DateTime lastActivityAt,
        string title = "seeded",
        params (string Role, string Content)[] items)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agent = await db.Set<Agent>().IgnoreQueryFilters()
            .FirstAsync(a => a.TenantId == TenantTestDefaults.DevelopmentTenantId && a.IsDefault);

        var conversation = Conversation.Create(TenantTestDefaults.DevelopmentTenantId, userId, agent.Id, title, lastActivityAt);
        foreach (var (role, content) in items)
            conversation.Append(role, content, lastActivityAt);
        db.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    public static async Task<List<ConversationItem>> ItemsAsync(IServiceProvider provider, Guid conversationId)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ConversationItem>()
            .Where(i => i.ConversationId == conversationId)
            .OrderBy(i => i.Sequence)
            .ToListAsync();
    }

    public static async Task<List<Conversation>> AllConversationsAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Conversation>()
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    public static async Task<int> ItemCountAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ConversationItem>().CountAsync();
    }
}

internal sealed class SaveCounter
{
    private int _calls;

    public int Calls => _calls;

    public void Increment() => Interlocked.Increment(ref _calls);
}

internal sealed class CountingUnitOfWork(AppDbContext db, SaveCounter counter) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return db.SaveChangesAsync(cancellationToken);
    }
}
