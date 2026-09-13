using Api.Host.Security;
using Api.Shared;
using MediatR;

namespace Api.Features.Authorization;

public sealed record GetRoleQuery(Guid RoleId) : IQuery<RoleWithPermissionsOutput>;

public sealed class GetRoleHandler(IRoleRepository roleRepository)
    : IRequestHandler<GetRoleQuery, RoleWithPermissionsOutput>
{
    public async Task<RoleWithPermissionsOutput> Handle(
        GetRoleQuery request,
        CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetWithPermissionsAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        return role.ToOutputWithPermissions();
    }
}

public sealed class GetRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/authorization/roles/{roleId:guid}", async (
            Guid roleId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetRoleQuery(roleId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesRead)
        .Produces<RoleWithPermissionsOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/authorization/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetRoleQuery(roleId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetRolePermissions")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesRead)
        .Produces<RoleWithPermissionsOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
