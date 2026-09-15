using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Identity;

public sealed class User : AggregateRoot, IMultiTenantEntity
{
    public Guid TenantId { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public string SecurityStamp { get; private set; } = Guid.NewGuid().ToString("N");
    public DateTime? LastLoginAt { get; private set; }

    private User() { }

    public static User Create(
        Guid tenantId,
        Email email,
        string passwordHash,
        string firstName,
        string lastName)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        return new User
        {
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };
    }
    public void UpdateLastLogin() => LastLoginAt = DateTime.UtcNow;

    public void RegenerateSecurityStamp() => SecurityStamp = Guid.NewGuid().ToString("N");

    public void UpdateProfile(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name cannot be empty.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name cannot be empty.", nameof(lastName));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public void Deactivate() => IsActive = false;
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<User>> ListAllAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<User>(), id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Email.Create(email);
        return db.Set<User>().FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<PaginatedListOutput<User>> ListAllAsync(
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var query = db.Set<User>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(listQuery.SearchTerm))
        {
            var term = listQuery.SearchTerm.Trim();
            query = query.Where(u => u.FirstName.Contains(term) || u.LastName.Contains(term));

            // Npgsql cannot query through Email.Value or EF.Property<string> on the conversion.
            if (term.Contains('@', StringComparison.Ordinal))
            {
                try
                {
                    var email = Email.Create(term);
                    query = db.Set<User>().Where(u =>
                        u.Email == email || u.FirstName.Contains(term) || u.LastName.Contains(term));
                }
                catch (ArgumentException)
                {
                    // Partial search strings that contain '@' but are not an email.
                }
            }
        }

        query = ApplySort(query, listQuery.SortBy, listQuery.SortDirection);

        return await query.ToPaginatedListAsync(listQuery, cancellationToken);
    }

    private static IQueryable<User> ApplySort(IQueryable<User> query, string? sortBy, string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id);

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "email" => descending
                ? query.OrderByDescending(u => u.Email).ThenBy(u => u.Id)
                : query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            "firstname" => descending
                ? query.OrderByDescending(u => u.FirstName).ThenBy(u => u.Id)
                : query.OrderBy(u => u.FirstName).ThenBy(u => u.Id),
            "lastname" => descending
                ? query.OrderByDescending(u => u.LastName).ThenBy(u => u.Id)
                : query.OrderBy(u => u.LastName).ThenBy(u => u.Id),
            "createdat" => descending
                ? query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
                : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            _ => query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
        };
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<User>(), user, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        EfRepositoryHelpers.Update(db.Set<User>(), user);
        return Task.CompletedTask;
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(u => u.Id);
        entity.Property(u => u.Email).HasConversion(e => e.Value, v => Email.Create(v)).HasMaxLength(255);
        entity.Property(u => u.PasswordHash).HasMaxLength(500);
        entity.Property(u => u.FirstName).HasMaxLength(100);
        entity.Property(u => u.LastName).HasMaxLength(100);
        entity.Property(u => u.SecurityStamp).HasMaxLength(64);
        entity.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
    }
}
