using Api.Host.Security;
using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Authorization;

public sealed record ListPermissionsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null)
    : ListQuery(PageNumber, PageSize, SearchTerm, SortBy, SortDirection),
        IQuery<PaginatedListOutput<PermissionOutput>>;

public sealed class ListPermissionsQueryValidator : ListQueryValidator<ListPermissionsQuery>;

public sealed class ListPermissionsHandler(IPermissionRepository permissionRepository)
    : IRequestHandler<ListPermissionsQuery, PaginatedListOutput<PermissionOutput>>
{
    public async Task<PaginatedListOutput<PermissionOutput>> Handle(
        ListPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await permissionRepository.ListAllAsync(request, cancellationToken);
        return new PaginatedListOutput<PermissionOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(p => p.ToOutput()).ToList());
    }
}

public sealed class ListPermissionsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/authorization/permissions", async (
            [AsParameters] ListPermissionsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListPermissions")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationPermissionsRead)
        .Produces<PaginatedListOutput<PermissionOutput>>(StatusCodes.Status200OK);
    }
}
