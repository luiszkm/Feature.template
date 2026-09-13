using Api.Host.Security;
using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Authorization;

public sealed record AssignPermissionToRoleCommand(Guid RoleId, Guid PermissionId) : ICommand<bool>;

public sealed class AssignPermissionToRoleValidator : AbstractValidator<AssignPermissionToRoleCommand>
{
    public AssignPermissionToRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionId).NotEmpty();
    }
}

public sealed class AssignPermissionToRoleHandler(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext) : IRequestHandler<AssignPermissionToRoleCommand, bool>
{
    public async Task<bool> Handle(
        AssignPermissionToRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before assigning permissions.");

        var role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        var permission = await permissionRepository.GetByIdAsync(request.PermissionId, cancellationToken)
            ?? throw new NotFoundException($"Permission '{request.PermissionId}' was not found.");

        await roleRepository.AssignPermissionAsync(role.Id, permission.Id, tenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed class AssignPermissionToRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/authorization/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            AssignPermissionToRoleRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new AssignPermissionToRoleCommand(roleId, body.PermissionId),
                cancellationToken);
            return Results.NoContent();
        })
        .WithName("AssignPermissionToRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record AssignPermissionToRoleRequest(Guid PermissionId);
