using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Api.Shared;

public record ListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null);

public sealed record PaginatedListOutput<T>(
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyList<T> Data);

public abstract class ListQueryValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : ListQuery
{
    protected ListQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

internal static class QueryablePagination
{
    public static async Task<PaginatedListOutput<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> query,
        ListQuery listQuery,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var data = await query
            .Skip((listQuery.PageNumber - 1) * listQuery.PageSize)
            .Take(listQuery.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedListOutput<T>(
            listQuery.PageNumber,
            listQuery.PageSize,
            totalCount,
            data);
    }
}
