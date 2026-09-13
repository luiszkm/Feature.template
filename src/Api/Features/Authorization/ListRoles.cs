using Api.Host.Security;
using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Authorization;

public sealed record ListRolesQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null)
    : ListQuery(PageNumber, PageSize, SearchTerm, SortBy, SortDirection),
        IQuery<PaginatedListOutput<RoleOutput>>;

public sealed class ListRolesQueryValidator : ListQueryValidator<ListRolesQuery>;

public sealed class ListRolesHandler(IRoleRepository roleRepository)
    : IRequestHandler<ListRolesQuery, PaginatedListOutput<RoleOutput>>
{
    public async Task<PaginatedListOutput<RoleOutput>> Handle(
        ListRolesQuery request,
        CancellationToken cancellationToken)
    {
        var page = await roleRepository.ListAllAsync(request, cancellationToken);
        return new PaginatedListOutput<RoleOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(r => new RoleOutput(r.Id, r.Name, r.Description)).ToList());
    }
}

public sealed class ListRolesEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/authorization/roles", async (
            [AsParameters] ListRolesQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListRoles")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesRead)
        .Produces<PaginatedListOutput<RoleOutput>>(StatusCodes.Status200OK);
    }
}
