using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListConversationsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortDirection = null)
    : ListQuery(PageNumber, PageSize, SearchTerm, SortBy, SortDirection),
        IQuery<PaginatedListOutput<ConversationSummary>>;

public sealed class ListConversationsQueryValidator : ListQueryValidator<ListConversationsQuery>;

public sealed class ListConversationsHandler(IConversationRepository conversations)
    : IRequestHandler<ListConversationsQuery, PaginatedListOutput<ConversationSummary>>
{
    public Task<PaginatedListOutput<ConversationSummary>> Handle(
        ListConversationsQuery request,
        CancellationToken cancellationToken) =>
        conversations.ListOwnedAsync(request, cancellationToken);
}

public sealed class ListConversationsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/conversations", async (
            [AsParameters] ListConversationsQuery query,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListConversations")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.Authenticated)
        .Produces<PaginatedListOutput<ConversationSummary>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
