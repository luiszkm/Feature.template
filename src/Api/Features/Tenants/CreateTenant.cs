using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Tenants;

public sealed record CreateTenantCommand(
    string TenantKey,
    string DisplayName,
    string? ContactEmail,
    TenantIsolationMode IsolationMode) : ICommand<TenantOutput>, ITenantExemptRequest;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.TenantKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.IsolationMode).IsInEnum();
        RuleFor(x => x.IsolationMode)
            .Equal(TenantIsolationMode.SharedDb)
            .WithMessage(
                "Only SharedDb isolation is supported. SchemaPerTenant and DedicatedDb are not implemented.");
    }
}

public sealed class CreateTenantHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork,
    IEnumerable<IDefaultAgentProvisioner> agentProvisioners) : IRequestHandler<CreateTenantCommand, TenantOutput>
{
    public async Task<TenantOutput> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (request.IsolationMode != TenantIsolationMode.SharedDb)
        {
            throw new BusinessRuleException(
                $"Tenant isolation mode '{request.IsolationMode}' is not implemented. " +
                $"Only '{TenantIsolationMode.SharedDb}' is supported.");
        }

        var existing = await tenantRepository.GetByKeyAsync(request.TenantKey, cancellationToken);
        if (existing is not null)
            throw new BusinessRuleException($"Tenant with key '{request.TenantKey}' already exists.");

        var tenant = Tenant.Create(
            request.TenantKey,
            request.DisplayName,
            request.ContactEmail,
            request.IsolationMode);

        await tenantRepository.AddAsync(tenant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var provisioner in agentProvisioners)
            await provisioner.EnsureDefaultAgentAsync(tenant.Id, cancellationToken);

        return TenantMapper.ToOutput(tenant);
    }
}

public sealed class CreateTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/tenants", async (
            CreateTenantCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/tenants/{result.TenantId}", result);
        })
        .WithName("CreateTenant")
        .WithTags("Tenants")
        .RequireAuthorization(SecurityPolicies.TenantsManage)
        .Produces<TenantOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
