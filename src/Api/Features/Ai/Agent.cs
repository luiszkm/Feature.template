using System.Text.Json;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

public sealed class Agent : AggregateRoot, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Instructions { get; private set; } = string.Empty;
    public List<string> ToolNames { get; private set; } = [];
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }

    private Agent() { }

    public static Agent Create(
        Guid tenantId,
        string name,
        string instructions,
        IReadOnlyList<string> toolNames,
        bool isDefault = false)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Instructions cannot be empty.", nameof(instructions));

        return new Agent
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Instructions = instructions.Trim(),
            ToolNames = [.. toolNames],
            IsActive = true,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string instructions, IReadOnlyList<string> toolNames)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Instructions cannot be empty.", nameof(instructions));

        Name = name.Trim();
        Instructions = instructions.Trim();
        ToolNames = [.. toolNames];
    }

    public void Deactivate() => IsActive = false;
}

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<Agent?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Agent?> GetDefaultAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<Agent>> ListAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(Agent agent, CancellationToken cancellationToken = default);
    Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default);
}

internal sealed class AgentRepository(AppDbContext db) : IAgentRepository
{
    public Task<Agent?> GetByIdAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<Agent>(), agentId, cancellationToken);

    public Task<Agent?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return db.Set<Agent>().FirstOrDefaultAsync(a => a.Name == normalized, cancellationToken);
    }

    public Task<Agent?> GetDefaultAsync(CancellationToken cancellationToken = default) =>
        db.Set<Agent>().FirstOrDefaultAsync(a => a.IsDefault && a.IsActive, cancellationToken);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        db.Set<Agent>().CountAsync(a => a.IsActive, cancellationToken);

    public async Task<PaginatedListOutput<Agent>> ListAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var query = db.Set<Agent>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(listQuery.SearchTerm))
        {
            var term = listQuery.SearchTerm.Trim();
            query = query.Where(a => a.Name.Contains(term) || a.Instructions.Contains(term));
        }

        query = ApplySort(query, listQuery.SortBy, listQuery.SortDirection);
        return await query.ToPaginatedListAsync(listQuery, cancellationToken);
    }

    private static IQueryable<Agent> ApplySort(IQueryable<Agent> query, string? sortBy, string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id);

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy.Trim().ToLowerInvariant() switch
        {
            "name" => descending
                ? query.OrderByDescending(a => a.Name).ThenBy(a => a.Id)
                : query.OrderBy(a => a.Name).ThenBy(a => a.Id),
            _ => query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
        };
    }

    public Task AddAsync(Agent agent, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<Agent>(), agent, cancellationToken);

    public Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        EfRepositoryHelpers.Update(db.Set<Agent>(), agent);
        return Task.CompletedTask;
    }
}

internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    private static readonly JsonSerializerOptions Json = new();

    public void Configure(EntityTypeBuilder<Agent> entity)
    {
        entity.ToTable("AiAgents");
        entity.HasKey(a => a.Id);
        entity.Property(a => a.Name).HasMaxLength(200).IsRequired();
        entity.Property(a => a.Instructions).HasMaxLength(4000).IsRequired();
        entity.Property(a => a.ToolNames)
            .HasConversion(
                names => JsonSerializer.Serialize(names, Json),
                json => JsonSerializer.Deserialize<List<string>>(json, Json) ?? new List<string>())
            .HasMaxLength(2000)
            .Metadata.SetValueComparer(
                new ValueComparer<List<string>>(
                    (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
                    names => names.Aggregate(0, (hash, name) => HashCode.Combine(hash, name.GetHashCode())),
                    names => names.ToList()));
        entity.HasIndex(a => new { a.TenantId, a.Name }).IsUnique();
    }
}
