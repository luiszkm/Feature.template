using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Tenants;

public sealed record DeactivateTenantCommand(Guid TenantId) : ICommand<bool>, ITenantExemptRequest;

public sealed class DeactivateTenantValidator : AbstractValidator<DeactivateTenantCommand>
{
    public DeactivateTenantValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class DeactivateTenantHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateTenantCommand, bool>
{
    public async Task<bool> Handle(DeactivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant '{request.TenantId}' was not found.");

        tenant.Deactivate();
        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed class DeactivateTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/tenants/{tenantId:guid}", async (
            Guid tenantId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeactivateTenantCommand(tenantId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeactivateTenant")
        .WithTags("Tenants")
        .RequireAuthorization(SecurityPolicies.TenantsManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
