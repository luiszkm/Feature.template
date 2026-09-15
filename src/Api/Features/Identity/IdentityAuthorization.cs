using System.Security.Claims;
using Api.Shared;
using Microsoft.AspNetCore.Authorization;

namespace Api.Features.Identity;

public sealed class SelfOrPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class SelfOrPermissionHandler : AuthorizationHandler<SelfOrPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SelfOrPermissionRequirement requirement)
    {
        if (context.User.IsInRole("Admin")
            || context.User.HasClaim(AuthorizationClaimTypes.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.Resource is HttpContext httpContext
            && httpContext.Request.RouteValues.TryGetValue("userId", out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var routeUserId))
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == routeUserId.ToString())
                context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
