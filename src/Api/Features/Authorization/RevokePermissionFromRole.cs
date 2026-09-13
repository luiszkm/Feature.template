using App.Host.Security;
using App.Shared;
using MediatR;

namespace App.Features.Authorization;

public sealed record RevokePermissionFromRoleCommand(Guid RoleId, Guid PermissionId) : ICommand<bool>;

public sealed class RevokePermissionFromRoleHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RevokePermissionFromRoleCommand, bool>
{
    public async Task<bool> Handle(
        RevokePermissionFromRoleCommand request,
        CancellationToken cancellationToken)
    {
        _ = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        await roleRepository.RevokePermissionAsync(request.RoleId, request.PermissionId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed class RevokePermissionFromRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/authorization/roles/{roleId:guid}/permissions/{permissionId:guid}", async (
            Guid roleId,
            Guid permissionId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new RevokePermissionFromRoleCommand(roleId, permissionId),
                cancellationToken);
            return Results.NoContent();
        })
        .WithName("RevokePermissionFromRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
