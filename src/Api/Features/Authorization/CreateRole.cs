using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Authorization;

public sealed record RoleOutput(Guid Id, string Name, string Description);

public sealed record CreateRoleCommand(string Name, string Description) : ICommand<RoleOutput>;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class CreateRoleHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext) : IRequestHandler<CreateRoleCommand, RoleOutput>
{
    public async Task<RoleOutput> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before creating roles.");

        var existing = await roleRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
            throw new BusinessRuleException($"Role '{request.Name}' already exists.");

        var role = Role.Create(tenantId, request.Name, request.Description);
        await roleRepository.AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RoleOutput(role.Id, role.Name, role.Description);
    }
}

public sealed class CreateRoleEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/authorization/roles", async (
            CreateRoleCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/authorization/roles/{result.Id}", result);
        })
        .WithName("CreateRole")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesManage)
        .Produces<RoleOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
