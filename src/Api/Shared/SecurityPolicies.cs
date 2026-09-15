namespace Api.Shared;

public static class SecurityPolicies
{
    public const string AuthorizationRolesRead = "AuthorizationRolesRead";
    public const string AuthorizationRolesManage = "AuthorizationRolesManage";
    public const string AuthorizationPermissionsRead = "AuthorizationPermissionsRead";
    public const string AuthorizationPermissionsManage = "AuthorizationPermissionsManage";
    public const string TenantsRead = "TenantsRead";
    public const string TenantsManage = "TenantsManage";
    public const string UsersRead = "UsersRead";
    public const string UsersManage = "UsersManage";
    public const string UserReadOrSelf = "UserReadOrSelf";
    public const string UserManageOrSelf = "UserManageOrSelf";
    public const string Authenticated = "Authenticated";
    public const string AiAgentsRead = "AiAgentsRead";
    public const string AiAgentsManage = "AiAgentsManage";
}

public static class RateLimitPolicies
{
    public const string AuthRateLimitPolicy = "auth";
}
