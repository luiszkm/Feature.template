using Microsoft.EntityFrameworkCore;

namespace App.Shared;

internal static class EfRepositoryHelpers
{
    public static Task<TEntity?> GetByIdAsync<TEntity>(
        DbSet<TEntity> set,
        Guid id,
        CancellationToken cancellationToken = default)
        where TEntity : Entity =>
        set.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public static async Task AddAsync<TEntity>(
        DbSet<TEntity> set,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : Entity
    {
        await set.AddAsync(entity, cancellationToken);
    }

    public static void Update<TEntity>(DbSet<TEntity> set, TEntity entity)
        where TEntity : Entity =>
        set.Update(entity);
}
