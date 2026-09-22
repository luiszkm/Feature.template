using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

/// <summary>One immutable row per paid LLM execution: metadata only, never prompt or reply.</summary>
public sealed class AiUsageEntry : AggregateRoot, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid AgentId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal? Cost { get; private set; }
    public long LatencyMs { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorCode { get; private set; }

    private AiUsageEntry() { }

    public static AiUsageEntry From(AiUsageRecord record) =>
        new()
        {
            TenantId = record.TenantId,
            AgentId = record.AgentId,
            Provider = record.Provider,
            Model = record.Model,
            Module = record.Module,
            Operation = record.Operation,
            InputTokens = record.InputTokens,
            OutputTokens = record.OutputTokens,
            Cost = record.Cost,
            LatencyMs = (long)record.Latency.TotalMilliseconds,
            Success = record.Success,
            ErrorCode = record.ErrorCode,
            CreatedAt = DateTime.UtcNow
        };
}

public static class AiUsageOperations
{
    public const string Chat = "chat";
    public const string Compare = "compare";
}

/// <summary>Append-only: add and read, no update or delete.</summary>
public interface IAiUsageRepository
{
    Task AddAsync(AiUsageEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiUsageEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task<long> SumTokensSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}

internal sealed class AiUsageRepository(AppDbContext db) : IAiUsageRepository
{
    public Task AddAsync(AiUsageEntry entry, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<AiUsageEntry>(), entry, cancellationToken);

    public async Task<IReadOnlyList<AiUsageEntry>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Set<AiUsageEntry>().AsNoTracking().OrderBy(e => e.CreatedAt).ToListAsync(cancellationToken);

    public async Task<long> SumTokensSinceAsync(DateTime since, CancellationToken cancellationToken = default) =>
        await db.Set<AiUsageEntry>()
            .Where(e => e.CreatedAt >= since)
            .SumAsync(e => (long)e.InputTokens + e.OutputTokens, cancellationToken);
}

internal sealed class AiUsageTracker(
    AppDbContext db,
    ILogger<AiUsageTracker> logger) : IAiUsageTracker
{
    public async Task TrackAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        var entry = AiUsageEntry.From(record);
        try
        {
            await db.Set<AiUsageEntry>().AddAsync(entry, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            db.Entry(entry).State = EntityState.Detached;
            logger.LogError(
                ex,
                "Failed to persist AI usage for tenant {TenantId}, agent {AgentId}, operation {Operation}",
                record.TenantId,
                record.AgentId,
                record.Operation);
        }
    }
}

internal sealed class AiUsageEntryConfiguration : IEntityTypeConfiguration<AiUsageEntry>
{
    public void Configure(EntityTypeBuilder<AiUsageEntry> entity)
    {
        entity.ToTable("AiUsageEntries");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Provider).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Model).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Operation).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Cost).HasPrecision(18, 8);
        entity.Property(e => e.ErrorCode).HasMaxLength(200);
        entity.HasIndex(e => new { e.TenantId, e.AgentId, e.CreatedAt });
    }
}
