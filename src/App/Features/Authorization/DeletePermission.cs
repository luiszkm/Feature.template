using App.Host.Security;
using App.Shared;
using MediatR;

namespace App.Features.Authorization;

public sealed record DeletePermissionCommand(Guid PermissionId) : ICommand<bool>;

public sealed class DeletePermissionHandler(
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeletePermissionCommand, bool>
{
    public async Task<bool> Handle(DeletePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await permissionRepository.GetByIdAsync(request.PermissionId, cancellationToken)
            ?? throw new NotFoundException($"Permission '{request.PermissionId}' was not found.");

        await permissionRepository.DeleteAsync(permission, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed class DeletePermissionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/authorization/permissions/{permissionId:guid}", async (
            Guid permissionId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeletePermissionCommand(permissionId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeletePermission")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationPermissionsManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
