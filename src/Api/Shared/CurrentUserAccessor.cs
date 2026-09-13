using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Api.Shared;

public interface ICurrentUserAccessor
{
    ClaimsPrincipal User { get; }
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
}

internal sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

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
