using App.Host.Security;
using App.Shared;
using MediatR;

namespace App.Features.Authorization;

public sealed record DeleteRoleCommand(Guid RoleId) : ICommand<bool>;

public sealed class DeleteRoleHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteRoleCommand, bool>
{
    public async Task<bool> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        await roleRepository.DeleteAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed class DeleteRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/authorization/roles/{roleId:guid}", async (
            Guid roleId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeleteRoleCommand(roleId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
