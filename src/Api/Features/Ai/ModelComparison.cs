using System.Text.Json;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

/// <summary>One run of an agent with a prompt across several models; results are written once.</summary>
public sealed class ModelComparison : AggregateRoot, IMultiTenantEntity
{
    private readonly List<ModelComparisonResult> _results = [];

    public Guid TenantId { get; private set; }
    public Guid AgentId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public List<ComparisonAttachment> Attachments { get; private set; } = [];
    public Guid? CreatedByUserId { get; private set; }
    public IReadOnlyList<ModelComparisonResult> Results => _results;

    private ModelComparison() { }

    public static ModelComparison Create(
        Guid tenantId,
        Guid agentId,
        string prompt,
        IReadOnlyList<ComparisonAttachment> attachments,
        Guid? createdByUserId,
        IReadOnlyList<ModelComparisonResult> results)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        var comparison = new ModelComparison
        {
            TenantId = tenantId,
            AgentId = agentId,
            Prompt = prompt,
            Attachments = [.. attachments],
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
        comparison._results.AddRange(results);
        return comparison;
    }

    public decimal? TotalCost =>
        _results.Any(r => r.Cost is not null) ? _results.Sum(r => r.Cost ?? 0m) : null;
}

public sealed record ComparisonAttachment(string Name, string Content);

public enum ModelComparisonStatus
{
    Succeeded,
    Failed,
    TimedOut
}

public sealed class ModelComparisonResult
{
    public int Position { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public ModelComparisonStatus Status { get; private set; }
    public string? Reply { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal? Cost { get; private set; }
    public long LatencyMs { get; private set; }
    public int IterationsUsed { get; private set; }
    public string? ErrorCode { get; private set; }

    private ModelComparisonResult() { }

    public static ModelComparisonResult Succeeded(int position, string model, AgentResult result, long latencyMs) =>
        new()
        {
            Position = position,
            Model = model,
            Status = ModelComparisonStatus.Succeeded,
            Reply = result.Reply,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            Cost = result.Cost,
            LatencyMs = latencyMs,
            IterationsUsed = result.IterationsUsed
        };

    public static ModelComparisonResult Unsuccessful(
        int position,
        string model,
        ModelComparisonStatus status,
        string errorCode,
        long latencyMs) =>
        new()
        {
            Position = position,
            Model = model,
            Status = status,
            ErrorCode = errorCode,
            LatencyMs = latencyMs
        };
}

public interface IModelComparisonRepository
{
    Task<ModelComparison?> GetByIdAsync(Guid comparisonId, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<ModelComparison>> ListAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(ModelComparison comparison, CancellationToken cancellationToken = default);
}

internal sealed class ModelComparisonRepository(AppDbContext db) : IModelComparisonRepository
{
    public Task<ModelComparison?> GetByIdAsync(Guid comparisonId, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<ModelComparison>(), comparisonId, cancellationToken);

    public Task<PaginatedListOutput<ModelComparison>> ListAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default) =>
        db.Set<ModelComparison>()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToPaginatedListAsync(listQuery, cancellationToken);

    public Task AddAsync(ModelComparison comparison, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<ModelComparison>(), comparison, cancellationToken);
}

internal sealed class ModelComparisonConfiguration : IEntityTypeConfiguration<ModelComparison>
{
    private static readonly JsonSerializerOptions Json = new();

    public void Configure(EntityTypeBuilder<ModelComparison> entity)
    {
        entity.ToTable("AiModelComparisons");
        entity.HasKey(c => c.Id);
        entity.Property(c => c.Prompt).HasMaxLength(4000).IsRequired();
        entity.Property(c => c.Attachments)
            .HasConversion(
                items => JsonSerializer.Serialize(items, Json),
                json => JsonSerializer.Deserialize<List<ComparisonAttachment>>(json, Json) ?? new List<ComparisonAttachment>())
            .Metadata.SetValueComparer(
                new ValueComparer<List<ComparisonAttachment>>(
                    (left, right) => (left ?? new List<ComparisonAttachment>()).SequenceEqual(right ?? new List<ComparisonAttachment>()),
                    items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    items => items.ToList()));
        entity.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(c => c.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(c => new { c.TenantId, c.CreatedAt });
        entity.Ignore(c => c.TotalCost);

        entity.OwnsMany(c => c.Results, result =>
        {
            result.ToTable("AiModelComparisonResults");
            result.WithOwner().HasForeignKey("ComparisonId");
            result.Property<int>("Id");
            result.HasKey("Id");
            result.Property(r => r.Model).HasMaxLength(200).IsRequired();
            result.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            result.Property(r => r.Cost).HasPrecision(18, 8);
            result.Property(r => r.ErrorCode).HasMaxLength(200);
        });
        entity.Navigation(c => c.Results)
            .HasField("_results")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
