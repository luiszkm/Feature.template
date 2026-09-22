using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

/// <summary>Server-owned transcript of one tenant's user with one agent.</summary>
public sealed class Conversation : AggregateRoot, IMultiTenantEntity
{
    public const int TitleMaxChars = 80;

    private readonly List<ConversationItem> _items = [];

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AgentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime LastActivityAt { get; private set; }
    public IReadOnlyList<ConversationItem> Items => _items;

    private Conversation() { }

    public static Conversation Create(Guid tenantId, Guid userId, Guid agentId, string firstMessage, DateTime now)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");
        if (userId == Guid.Empty)
            throw new BusinessRuleException("UserId is required.");

        return new Conversation
        {
            TenantId = tenantId,
            UserId = userId,
            AgentId = agentId,
            Title = DeriveTitle(firstMessage),
            LastActivityAt = now,
            CreatedAt = now
        };
    }

    public static string DeriveTitle(string message)
    {
        var trimmed = message.Trim();
        return trimmed.Length <= TitleMaxChars ? trimmed : trimmed[..TitleMaxChars] + "…";
    }

    public ConversationItem Append(string role, string content, DateTime now)
    {
        var next = _items.Count == 0 ? 1 : _items.Max(item => item.Sequence) + 1;
        var item = ConversationItem.Create(Id, next, role, content, now);
        _items.Add(item);
        LastActivityAt = now;
        return item;
    }

    /// <summary>
    /// The turns replayed to the model: text <c>user</c>/<c>assistant</c> items only, the last
    /// <paramref name="window"/> of them, oldest first. Tool turns stay stored and are not resent,
    /// so the window can never split a tool call from its result.
    /// </summary>
    public IReadOnlyList<LlmMessage> HistoryWindow(int window) =>
        _items
            .Where(item => item.Role is ConversationRoles.User or ConversationRoles.Assistant)
            .Where(item => item.Content.Length > 0)
            .OrderBy(item => item.Sequence)
            .TakeLast(window)
            .Select(item => new LlmMessage(item.Role, item.Content))
            .ToList();
}

public sealed record ConversationSummary(
    Guid ConversationId,
    string Title,
    Guid AgentId,
    DateTime LastActivityAt,
    int ItemCount);

public interface IConversationRepository
{
    /// <summary>The caller's conversation with its items, or <c>null</c> when it is missing or someone else's.</summary>
    Task<Conversation?> GetOwnedAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<ConversationSummary>> ListOwnedAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    void AddItem(ConversationItem item);
    void Delete(Conversation conversation);
    /// <summary>Drops a turn whose SaveChanges failed, so the usage row after it can still be written.</summary>
    void DiscardChanges();
}

/// <summary>Ownership lives here: every query carries the tenant filter and the caller's user id, Admin included.</summary>
internal sealed class ConversationRepository(AppDbContext db, ICurrentUserAccessor currentUser) : IConversationRepository
{
    public Task<Conversation?> GetOwnedAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Owned()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    public async Task<PaginatedListOutput<ConversationSummary>> ListOwnedAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var query = Owned().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(listQuery.SearchTerm))
        {
            var term = listQuery.SearchTerm.Trim();
            query = query.Where(c => c.Title.Contains(term));
        }

        query = ApplySort(query, listQuery.SortBy, listQuery.SortDirection);

        return await query
            .Select(c => new ConversationSummary(c.Id, c.Title, c.AgentId, c.LastActivityAt, c.Items.Count))
            .ToPaginatedListAsync(listQuery, cancellationToken);
    }

    private static IQueryable<Conversation> ApplySort(IQueryable<Conversation> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(c => c.Title).ThenBy(c => c.Id)
                : query.OrderBy(c => c.Title).ThenBy(c => c.Id),
            "lastactivityat" when !descending => query.OrderBy(c => c.LastActivityAt).ThenBy(c => c.Id),
            _ => query.OrderByDescending(c => c.LastActivityAt).ThenBy(c => c.Id)
        };
    }

    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<Conversation>(), conversation, cancellationToken);

    public void AddItem(ConversationItem item) => db.Set<ConversationItem>().Add(item);

    public void Delete(Conversation conversation) => db.Set<Conversation>().Remove(conversation);

    public void DiscardChanges() => db.ChangeTracker.Clear();

    private IQueryable<Conversation> Owned()
    {
        var userId = currentUser.UserId;
        return userId is { } id
            ? db.Set<Conversation>().Where(c => c.UserId == id)
            : db.Set<Conversation>().Where(_ => false);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> entity)
    {
        entity.ToTable("AiConversations");
        entity.HasKey(c => c.Id);
        entity.Property(c => c.Title).HasMaxLength(200).IsRequired();
        // Two turns appending to the same conversation both rewrite this value; the second
        // SaveChanges fails instead of interleaving its items with the first one's.
        entity.Property(c => c.LastActivityAt).IsConcurrencyToken();
        entity.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(c => c.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(c => c.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.HasIndex(c => new { c.TenantId, c.UserId, c.LastActivityAt });
    }
}
