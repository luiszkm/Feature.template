using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Tenants;

public sealed record ListTenantsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null)
    : ListQuery(PageNumber, PageSize, SearchTerm, SortBy, SortDirection),
        IQuery<PaginatedListOutput<TenantOutput>>;

public sealed class ListTenantsQueryValidator : ListQueryValidator<ListTenantsQuery>;

public sealed class ListTenantsHandler(ITenantRepository tenantRepository)
    : IRequestHandler<ListTenantsQuery, PaginatedListOutput<TenantOutput>>
{
    public async Task<PaginatedListOutput<TenantOutput>> Handle(
        ListTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await tenantRepository.ListAllAsync(request, cancellationToken);
        return new PaginatedListOutput<TenantOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(TenantMapper.ToOutput).ToList());
    }
}

public sealed class ListTenantsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenants", async (
            [AsParameters] ListTenantsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListTenants")
        .WithTags("Tenants")
        .RequireAuthorization(SecurityPolicies.TenantsRead)
        .Produces<PaginatedListOutput<TenantOutput>>(StatusCodes.Status200OK);
    }
}
