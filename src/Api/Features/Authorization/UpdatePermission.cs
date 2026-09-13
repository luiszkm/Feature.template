using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Authorization;

public sealed record UpdatePermissionCommand(Guid PermissionId, string Name, string Description)
    : ICommand<PermissionOutput>;

public sealed class UpdatePermissionValidator : AbstractValidator<UpdatePermissionCommand>
{
    public UpdatePermissionValidator()
    {
        RuleFor(x => x.PermissionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class UpdatePermissionHandler(
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdatePermissionCommand, PermissionOutput>
{
    public async Task<PermissionOutput> Handle(
        UpdatePermissionCommand request,
        CancellationToken cancellationToken)
    {
        var permission = await permissionRepository.GetByIdAsync(request.PermissionId, cancellationToken)
            ?? throw new NotFoundException($"Permission '{request.PermissionId}' was not found.");

        permission.Update(request.Name, request.Description);
        await permissionRepository.UpdateAsync(permission, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return permission.ToOutput();
    }
}

public sealed class UpdatePermissionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/authorization/permissions/{permissionId:guid}", async (
            Guid permissionId,
            UpdatePermissionRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new UpdatePermissionCommand(permissionId, body.Name, body.Description),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdatePermission")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationPermissionsManage)
        .Produces<PermissionOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdatePermissionRequest(string Name, string Description);
