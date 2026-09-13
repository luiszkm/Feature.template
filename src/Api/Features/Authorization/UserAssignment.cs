using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Authorization;

public sealed class UserAssignment : AggregateRoot, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    private UserAssignment() { }

    public static UserAssignment Create(Guid userId, Guid roleId, Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        if (userId == Guid.Empty)
            throw new BusinessRuleException("UserId is required.");

        if (roleId == Guid.Empty)
            throw new BusinessRuleException("RoleId is required.");

        return new UserAssignment
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public interface IUserAssignmentRepository
{
    Task<UserAssignment?> GetByUserAndRoleAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAssignment>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task AddAsync(UserAssignment assignment, CancellationToken cancellationToken = default);
    Task DeleteAsync(UserAssignment assignment, CancellationToken cancellationToken = default);
}

internal sealed class UserAssignmentRepository(AppDbContext db) : IUserAssignmentRepository
{
    public Task<UserAssignment?> GetByUserAndRoleAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        db.Set<UserAssignment>().FirstOrDefaultAsync(
            ua => ua.UserId == userId && ua.RoleId == roleId,
            cancellationToken);

    public async Task<IReadOnlyList<UserAssignment>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await db.Set<UserAssignment>()
            .Where(ua => ua.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task AddAsync(UserAssignment assignment, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<UserAssignment>(), assignment, cancellationToken);

    public Task DeleteAsync(UserAssignment assignment, CancellationToken cancellationToken = default)
    {
        db.Set<UserAssignment>().Remove(assignment);
        return Task.CompletedTask;
    }
}

internal sealed class UserAssignmentConfiguration : IEntityTypeConfiguration<UserAssignment>
{
    public void Configure(EntityTypeBuilder<UserAssignment> entity)
    {
        entity.ToTable("UserAssignments");
        entity.HasKey(ua => ua.Id);
        entity.HasIndex(ua => new { ua.TenantId, ua.UserId, ua.RoleId }).IsUnique();
    }
}
