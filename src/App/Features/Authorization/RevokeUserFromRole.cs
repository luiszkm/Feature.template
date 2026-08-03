using App.Features.Identity;
using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Authorization;

public sealed record RevokeUserFromRoleCommand(Guid UserId, Guid RoleId) : ICommand<bool>;

public sealed class RevokeUserFromRoleValidator : AbstractValidator<RevokeUserFromRoleCommand>
{
    public RevokeUserFromRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class RevokeUserFromRoleHandler(
    IUserAssignmentRepository assignmentRepository,
    ISecurityStampService securityStampService,
    ITenantContext tenantContext) : IRequestHandler<RevokeUserFromRoleCommand, bool>
{
    public async Task<bool> Handle(RevokeUserFromRoleCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByUserAndRoleAsync(
            request.UserId,
            request.RoleId,
            cancellationToken)
            ?? throw new NotFoundException(
                $"User '{request.UserId}' is not assigned to role '{request.RoleId}'.");

        await assignmentRepository.DeleteAsync(assignment, cancellationToken);

        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before revoking roles.");

        await securityStampService.RegenerateAsync(tenantId, request.UserId, cancellationToken);

        return true;
    }
}

public sealed class RevokeUserFromRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/authorization/users/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId,
            Guid roleId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new RevokeUserFromRoleCommand(userId, roleId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("RevokeUserFromRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
