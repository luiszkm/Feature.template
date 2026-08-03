using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Identity;

public sealed record ListUsersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null)
    : ListQuery(PageNumber, PageSize, SearchTerm, SortBy, SortDirection),
        IQuery<PaginatedListOutput<UserOutput>>;

public sealed class ListUsersQueryValidator : ListQueryValidator<ListUsersQuery>;

public sealed class ListUsersHandler(IUserRepository userRepository)
    : IRequestHandler<ListUsersQuery, PaginatedListOutput<UserOutput>>
{
    public async Task<PaginatedListOutput<UserOutput>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
    {
        var page = await userRepository.ListAllAsync(request, cancellationToken);
        return new PaginatedListOutput<UserOutput>(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.Data.Select(u => u.ToOutput()).ToList());
    }
}

public sealed class ListUsersEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/identity/users", async (
            [AsParameters] ListUsersQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListUsers")
        .WithTags("Identity")
        .RequireAuthorization(SecurityPolicies.UsersRead)
        .Produces<PaginatedListOutput<UserOutput>>(StatusCodes.Status200OK);
    }
}
