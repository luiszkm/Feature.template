using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Tenants;

public sealed record UpdateTenantCommand(
    Guid TenantId,
    string DisplayName,
    string? ContactEmail) : ICommand<TenantOutput>, ITenantExemptRequest;

public sealed class UpdateTenantValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}

public sealed class UpdateTenantHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateTenantCommand, TenantOutput>
{
    public async Task<TenantOutput> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant '{request.TenantId}' was not found.");

        tenant.Update(request.DisplayName, request.ContactEmail);
        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TenantMapper.ToOutput(tenant);
    }
}

public sealed class UpdateTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/tenants/{tenantId:guid}", async (
            Guid tenantId,
            UpdateTenantRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new UpdateTenantCommand(tenantId, body.DisplayName, body.ContactEmail),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateTenant")
        .WithTags("Tenants")
        .RequireAuthorization(SecurityPolicies.TenantsManage)
        .Produces<TenantOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateTenantRequest(string DisplayName, string? ContactEmail);
