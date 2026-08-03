using App.Features.Authorization;
using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Authorization;

public sealed record PermissionOutput(Guid Id, string Name, string Description);

public sealed record CreatePermissionCommand(string Name, string Description) : ICommand<PermissionOutput>;

public sealed class CreatePermissionValidator : AbstractValidator<CreatePermissionCommand>
{
    public CreatePermissionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class CreatePermissionHandler(
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext) : IRequestHandler<CreatePermissionCommand, PermissionOutput>
{
    public async Task<PermissionOutput> Handle(
        CreatePermissionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before creating permissions.");

        var existing = await permissionRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
            throw new BusinessRuleException($"Permission '{request.Name}' already exists.");

        var permission = Permission.Create(tenantId, request.Name, request.Description);
        await permissionRepository.AddAsync(permission, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PermissionOutput(permission.Id, permission.Name, permission.Description);
    }
}

public sealed class CreatePermissionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/authorization/permissions", async (
            CreatePermissionCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/authorization/permissions/{result.Id}", result);
        })
        .WithName("CreatePermission")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationPermissionsManage)
        .Produces<PermissionOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
