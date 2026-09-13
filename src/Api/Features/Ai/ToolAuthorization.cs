using System.Security.Claims;
using App.Shared;

namespace App.Features.Ai;

internal static class ToolAuthorization
{
    public static void EnsurePermission(ICurrentUserAccessor currentUser, string permissionCode)
    {
        var isAdmin = currentUser.User.IsInRole("Admin");
        var hasPermission = currentUser.User.HasClaim(
            AuthorizationClaimTypes.Permission,
            permissionCode);

        if (!isAdmin && !hasPermission)
        {
            throw new UnauthorizedAccessException(
                $"Current user lacks permission '{permissionCode}' required by this tool.");
        }
    }
}
