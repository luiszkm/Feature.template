using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed class AiRateLimitOptions
{
    public const string SectionName = "Ai:RateLimit";

    public int PermitLimit { get; set; } = 30;
    public int WindowSeconds { get; set; } = 60;
}

public sealed class AiQuotaOptions
{
    public const string SectionName = "Ai:Quota";

    /// <summary>Input + output tokens per tenant per UTC day; 0 turns the quota off.</summary>
    public long DailyTokensPerTenant { get; set; }
}

/// <summary>
/// One fixed-window bucket per tenant across every route that spends LLM tokens. Runs after
/// authentication and tenant resolution, so the key is never taken from a caller-controlled header.
/// </summary>
public sealed class AiRateLimitPolicy(IOptions<AiRateLimitOptions> options) : IRateLimiterPolicy<string>
{
    public const string NoTenantKey = "none";
    public const string RejectedTitle = "AI rate limit exceeded";
    public const string RejectedDetail = "Limite de pedidos de IA do tenant atingido. Tente novamente dentro de instantes.";

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } =
        async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Title = RejectedTitle,
                    Detail = RejectedDetail,
                    Status = StatusCodes.Status429TooManyRequests
                },
                cancellationToken);
        };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var value = options.Value;
        return RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = value.PermitLimit,
            Window = TimeSpan.FromSeconds(value.WindowSeconds),
            QueueLimit = 0
        });
    }

    public static string PartitionKey(HttpContext httpContext) =>
        httpContext.RequestServices.GetService<ITenantContext>()?.TenantId is { } tenantId
            ? tenantId.ToString("N")
            : NoTenantKey;
}

public sealed class AiQuota(IAiUsageRepository usage, IOptions<AiQuotaOptions> options)
{
    public const string ExceededTitle = "AI quota exceeded";
    public const string ExceededDetail = "Limite diário de tokens de IA do tenant atingido.";

    /// <summary>
    /// Soft cap: the ledger is read before the run writes to it, so concurrent requests can each
    /// overshoot by one execution.
    /// </summary>
    public async Task EnsureWithinAsync(CancellationToken cancellationToken)
    {
        var limit = options.Value.DailyTokensPerTenant;
        if (limit <= 0)
            return;

        var spent = await usage.SumTokensSinceAsync(DateTime.UtcNow.Date, cancellationToken);
        if (spent >= limit)
            throw new TooManyRequestsException(ExceededTitle, ExceededDetail);
    }
}
