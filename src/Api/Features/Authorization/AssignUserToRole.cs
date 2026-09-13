using Api.Features.Identity;
using Api.Host.Security;
using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Authorization;

public sealed record AssignUserToRoleCommand(Guid UserId, Guid RoleId) : ICommand<bool>;

public sealed class AssignUserToRoleValidator : AbstractValidator<AssignUserToRoleCommand>
{
    public AssignUserToRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class AssignUserToRoleHandler(
    IUserAssignmentRepository assignmentRepository,
    IRoleRepository roleRepository,
    IUserRepository userRepository,
    ISecurityStampService securityStampService,
    ITenantContext tenantContext) : IRequestHandler<AssignUserToRoleCommand, bool>
{
    public async Task<bool> Handle(AssignUserToRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before assigning roles.");

        var role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        var existing = await assignmentRepository.GetByUserAndRoleAsync(
            request.UserId,
            request.RoleId,
            cancellationToken);

        if (existing is not null)
            return true;

        var assignment = UserAssignment.Create(request.UserId, request.RoleId, tenantId);
        await assignmentRepository.AddAsync(assignment, cancellationToken);

        await securityStampService.RegenerateAsync(tenantId, request.UserId, cancellationToken);

        return true;
    }
}

public sealed class AssignUserToRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/authorization/users/{userId:guid}/roles", async (
            Guid userId,
            AssignUserToRoleRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new AssignUserToRoleCommand(userId, body.RoleId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("AssignUserToRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record AssignUserToRoleRequest(Guid RoleId);
