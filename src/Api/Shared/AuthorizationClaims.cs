namespace Api.Shared;

public static class AuthorizationClaimTypes
{
    public const string Permission = "permission";
    public const string SecurityStamp = "security_stamp";
    public const string TenantId = "tenant_id";
}

public interface IUserRolesProvider
{
    Task<UserRolesData> GetUserRolesAndPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record UserRolesData(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
