using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Authorization;

public sealed record UpdateRoleCommand(Guid RoleId, string Name, string Description) : ICommand<RoleOutput>;

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class UpdateRoleHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateRoleCommand, RoleOutput>
{
    public async Task<RoleOutput> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        role.Update(request.Name, request.Description);
        await roleRepository.UpdateAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return role.ToOutput();
    }
}

public sealed class UpdateRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/authorization/roles/{roleId:guid}", async (
            Guid roleId,
            UpdateRoleRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new UpdateRoleCommand(roleId, body.Name, body.Description),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces<RoleOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateRoleRequest(string Name, string Description);
