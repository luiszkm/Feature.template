namespace Api.Shared;

public sealed record UserDirectoryEntry(Guid Id, string Email, string FirstName, string LastName);

public interface IUserDirectory
{
    Task<PaginatedListOutput<UserDirectoryEntry>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record TenantDirectoryEntry(
    Guid TenantId,
    string TenantKey,
    TenantIsolationMode IsolationMode,
    bool IsActive);

public interface ITenantDirectory
{
    Task<PaginatedListOutput<TenantDirectoryEntry>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default);
}

public static class DirectoryPermissions
{
    public const string UsersRead = "identity.user.read";
    public const string TenantsRead = "tenants.read";
}

/// <summary>
/// Seeds the tenant-default prompt agent. Implemented by Ai; consumed by Tenants/Host.
/// </summary>
public interface IDefaultAgentProvisioner
{
    Task EnsureDefaultAgentAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
