using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

public sealed class AgentFile : AggregateRoot, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid AgentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    private AgentFile() { }

    public static AgentFile Create(Guid tenantId, Guid agentId, string name, string content)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");
        if (agentId == Guid.Empty)
            throw new BusinessRuleException("AgentId is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new AgentFile
        {
            TenantId = tenantId,
            AgentId = agentId,
            Name = name.Trim(),
            Content = content ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public interface IAgentFileRepository
{
    Task<AgentFile?> GetByIdAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentFile>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task AddAsync(AgentFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(AgentFile file, CancellationToken cancellationToken = default);
}

internal sealed class AgentFileRepository(AppDbContext db) : IAgentFileRepository
{
    public Task<AgentFile?> GetByIdAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<AgentFile>(), fileId, cancellationToken);

    public async Task<IReadOnlyList<AgentFile>> ListByAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default) =>
        await db.Set<AgentFile>()
            .Where(file => file.AgentId == agentId)
            .OrderByDescending(file => file.CreatedAt)
            .ThenBy(file => file.Id)
            .ToListAsync(cancellationToken);

    public Task AddAsync(AgentFile file, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<AgentFile>(), file, cancellationToken);

    public Task DeleteAsync(AgentFile file, CancellationToken cancellationToken = default)
    {
        db.Set<AgentFile>().Remove(file);
        return Task.CompletedTask;
    }
}

internal sealed class AgentFileConfiguration : IEntityTypeConfiguration<AgentFile>
{
    public void Configure(EntityTypeBuilder<AgentFile> entity)
    {
        entity.ToTable("AiAgentFiles");
        entity.HasKey(f => f.Id);
        entity.Property(f => f.Name).HasMaxLength(200).IsRequired();
        entity.Property(f => f.Content).IsRequired();
        entity.HasIndex(f => new { f.TenantId, f.AgentId });
        entity.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(f => f.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
