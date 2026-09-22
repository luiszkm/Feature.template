using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

/// <summary>
/// Deletes conversations idle for longer than <c>Ai:Conversations:RetentionDays</c>, across every
/// tenant. Runs outside any request, so it ignores the tenant query filter on purpose.
/// </summary>
internal sealed class ConversationRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<ConversationOptions> options,
    TimeProvider clock,
    ILogger<ConversationRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.PurgeIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            try
            {
                await Task.Delay(interval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One pass; a failure is logged and never stops the host or the next pass.</summary>
    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PurgeAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Conversation retention pass failed");
        }
    }

    internal async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var retentionDays = options.Value.RetentionDays;
        if (retentionDays <= 0)
            return 0;

        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expired = await db.Set<Conversation>()
            .IgnoreQueryFilters()
            .Include(c => c.Items)
            .Where(c => c.LastActivityAt < cutoff)
            .ToListAsync(cancellationToken);

        db.Set<Conversation>().RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Conversation retention deleted {Count} conversations older than {Cutoff}", expired.Count, cutoff);
        return expired.Count;
    }
}
