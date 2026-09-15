using Api.Shared;
using MediatR;

namespace Api.Features.Identity;

public sealed record GetUserRolesQuery(Guid UserId) : IQuery<IReadOnlyList<string>>;

public sealed class GetUserRolesHandler(IUserRolesProvider userRolesProvider)
    : IRequestHandler<GetUserRolesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetUserRolesQuery request,
        CancellationToken cancellationToken)
    {
        var rolesData = await userRolesProvider.GetUserRolesAndPermissionsAsync(
            request.UserId,
            cancellationToken);

        return rolesData.Roles;
    }
}

public sealed class GetUserRolesEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/identity/users/{userId:guid}/roles", async (
            Guid userId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetUserRolesQuery(userId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUserRoles")
        .WithTags("Identity")
        .RequireAuthorization(SecurityPolicies.UsersManage)
        .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK);
    }
}
