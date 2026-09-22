using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

public static class ConversationRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

/// <summary>One turn of a conversation, in the order the loop produced it.</summary>
public sealed class ConversationItem : Entity
{
    public Guid ConversationId { get; private set; }
    public int Sequence { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    private ConversationItem() { }

    internal static ConversationItem Create(Guid conversationId, int sequence, string role, string content, DateTime now) =>
        new()
        {
            ConversationId = conversationId,
            Sequence = sequence,
            Role = role,
            Content = content,
            CreatedAt = now
        };
}

internal sealed class ConversationItemConfiguration : IEntityTypeConfiguration<ConversationItem>
{
    public void Configure(EntityTypeBuilder<ConversationItem> entity)
    {
        entity.ToTable("AiConversationItems");
        entity.HasKey(i => i.Id);
        entity.Property(i => i.Id).ValueGeneratedNever();
        entity.Property(i => i.Role).HasMaxLength(20).IsRequired();
        entity.Property(i => i.Content).IsRequired();
        entity.HasIndex(i => new { i.ConversationId, i.Sequence }).IsUnique();
    }
}
