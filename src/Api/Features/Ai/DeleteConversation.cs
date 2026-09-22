using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record DeleteConversationCommand(Guid ConversationId) : ICommand<bool>;

public sealed class DeleteConversationValidator : AbstractValidator<DeleteConversationCommand>
{
    public DeleteConversationValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

public sealed class DeleteConversationHandler(
    IConversationRepository conversations,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteConversationCommand, bool>
{
    public async Task<bool> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetOwnedAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation '{request.ConversationId}' was not found.");

        conversations.Delete(conversation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class DeleteConversationEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/ai/conversations/{conversationId:guid}", async (
            Guid conversationId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeleteConversationCommand(conversationId), cancellationToken);
            return Results.NoContent();
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("DeleteConversation")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.Authenticated)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
