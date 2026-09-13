using App.Host.Security;
using App.Shared;
using MediatR;

namespace App.Features.Tenants;

public sealed record GetTenantQuery(Guid TenantId) : IQuery<TenantOutput>, ITenantExemptRequest;

public sealed class GetTenantHandler(ITenantRepository tenantRepository)
    : IRequestHandler<GetTenantQuery, TenantOutput>
{
    public async Task<TenantOutput> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant '{request.TenantId}' was not found.");

        return TenantMapper.ToOutput(tenant);
    }
}

public sealed class GetTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenants/{tenantId:guid}", async (
            Guid tenantId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetTenantQuery(tenantId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetTenant")
        .WithTags("Tenants")
        .RequireAuthorization(SecurityPolicies.TenantsRead)
        .Produces<TenantOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
