using System.Text.RegularExpressions;

namespace Api.Shared;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
}

public abstract class Entity : IAuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public abstract class AggregateRoot : Entity;

public interface IMultiTenantEntity
{
    Guid TenantId { get; }
}

public sealed class BusinessRuleException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);

public sealed record Email
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var normalized = email.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(normalized))
            throw new ArgumentException("Invalid email format.", nameof(email));

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
