using System.Security.Cryptography;
using System.Text;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Identity;

public sealed class RefreshToken : Entity, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hash of the raw refresh token. Never store the raw value.</summary>
    public string Token { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string? RevokedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string CreatedByIp { get; private set; } = string.Empty;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    public static string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    public static RefreshToken Create(
        Guid tenantId,
        Guid userId,
        string rawToken,
        int expirationDays,
        string createdByIp)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        return new RefreshToken
        {
            TenantId = tenantId,
            UserId = userId,
            Token = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            IsRevoked = false,
            CreatedByIp = createdByIp,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke(string revokedByIp, string? replacedByRawToken = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByToken = replacedByRawToken is null ? null : HashToken(replacedByRawToken);
    }
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetActiveByTokenAsync(string rawToken, CancellationToken cancellationToken = default);
    Task<bool> TryRevokeAsync(
        string rawToken,
        string revokedByIp,
        string? replacedByRawToken,
        CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetActiveByTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = RefreshToken.HashToken(rawToken);
        var now = DateTime.UtcNow;
        return db.Set<RefreshToken>().FirstOrDefaultAsync(
            rt => rt.Token == tokenHash && !rt.IsRevoked && rt.ExpiresAt > now,
            cancellationToken);
    }

    public async Task<bool> TryRevokeAsync(
        string rawToken,
        string revokedByIp,
        string? replacedByRawToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = RefreshToken.HashToken(rawToken);
        var replacedByHash = replacedByRawToken is null ? null : RefreshToken.HashToken(replacedByRawToken);
        var now = DateTime.UtcNow;

        if (db.Database.IsInMemory())
        {
            var tokenEntity = await db.Set<RefreshToken>().FirstOrDefaultAsync(
                rt => rt.Token == tokenHash && !rt.IsRevoked && rt.ExpiresAt > now,
                cancellationToken);

            if (tokenEntity is null)
                return false;

            tokenEntity.Revoke(revokedByIp, replacedByRawToken);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var rowsAffected = await db.Set<RefreshToken>()
            .Where(rt => rt.Token == tokenHash && !rt.IsRevoked && rt.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedAt, now)
                    .SetProperty(rt => rt.RevokedByIp, revokedByIp)
                    .SetProperty(rt => rt.ReplacedByToken, replacedByHash),
                cancellationToken);

        return rowsAffected > 0;
    }

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<RefreshToken>(), refreshToken, cancellationToken);
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("RefreshTokens");
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Token).HasMaxLength(500);
        entity.Property(t => t.CreatedByIp).HasMaxLength(64);
        entity.Property(t => t.RevokedByIp).HasMaxLength(64);
        entity.Property(t => t.ReplacedByToken).HasMaxLength(500);
        entity.HasIndex(t => new { t.TenantId, t.Token });
    }
}
