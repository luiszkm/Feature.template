using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetConversationQuery(Guid ConversationId, bool IncludeToolItems = false) : IQuery<ConversationOutput>;

public sealed record ConversationItemOutput(Guid ItemId, string Role, string Content, int Sequence, DateTime CreatedAt);

public sealed record ConversationOutput(
    Guid ConversationId,
    string Title,
    Guid AgentId,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    IReadOnlyList<ConversationItemOutput> Items);

public sealed class GetConversationValidator : AbstractValidator<GetConversationQuery>
{
    public GetConversationValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

public sealed class GetConversationHandler(IConversationRepository conversations)
    : IRequestHandler<GetConversationQuery, ConversationOutput>
{
    public async Task<ConversationOutput> Handle(GetConversationQuery request, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetOwnedAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        var items = conversation.Items
            .Where(item => request.IncludeToolItems || item.Role != ConversationRoles.Tool)
            .OrderBy(item => item.Sequence)
            .Select(item => new ConversationItemOutput(item.Id, item.Role, item.Content, item.Sequence, item.CreatedAt))
            .ToList();

        return new ConversationOutput(
            conversation.Id,
            conversation.Title,
            conversation.AgentId,
            conversation.CreatedAt,
            conversation.LastActivityAt,
            items);
    }
}

public sealed class GetConversationEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/conversations/{conversationId:guid}", async (
            Guid conversationId,
            bool? includeToolItems,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new GetConversationQuery(conversationId, includeToolItems ?? false),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetConversation")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.Authenticated)
        .Produces<ConversationOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
