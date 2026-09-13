using App.Shared;
using Microsoft.Extensions.Caching.Memory;

namespace App.Features.Identity;

public interface ISecurityStampService
{
    Task RegenerateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(Guid tenantId, Guid userId, string stamp, CancellationToken cancellationToken = default);
}

internal sealed class SecurityStampService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IMemoryCache cache) : ISecurityStampService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task RegenerateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        user.RegenerateSecurityStamp();
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKey(tenantId, userId));
    }

    public async Task<bool> ValidateAsync(
        Guid tenantId,
        Guid userId,
        string stamp,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey(tenantId, userId), out string? cachedStamp))
            return cachedStamp == stamp;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.TenantId != tenantId)
            return false;

        cache.Set(CacheKey(tenantId, userId), user.SecurityStamp, CacheTtl);
        return user.SecurityStamp == stamp;
    }

    private static string CacheKey(Guid tenantId, Guid userId) => $"security_stamp_{tenantId:N}_{userId:N}";
}
