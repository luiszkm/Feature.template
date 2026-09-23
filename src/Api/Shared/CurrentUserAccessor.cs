using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Api.Shared;

public interface ICurrentUserAccessor
{
    ClaimsPrincipal User { get; }
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
}

/// <summary>
/// The principal a background job acts as, set once per DI scope; outside such a scope it stays
/// empty and the request's user applies.
/// </summary>
public sealed class BackgroundPrincipal
{
    public ClaimsPrincipal? User { get; private set; }

    public void Set(ClaimsPrincipal user) => User = user;
}

internal sealed class CurrentUserAccessor(
    IHttpContextAccessor httpContextAccessor,
    BackgroundPrincipal? backgroundPrincipal = null) : ICurrentUserAccessor
{
    public ClaimsPrincipal User =>
        backgroundPrincipal?.User ?? httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
