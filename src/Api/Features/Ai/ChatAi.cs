using Api.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed record ChatAiCommand(
    string Message,
    IReadOnlyList<LlmMessage>? History = null,
    Guid? AgentId = null,
    Guid? ConversationId = null) : ICommand<ChatAiOutput>;

public sealed class ChatAiValidator : AbstractValidator<ChatAiCommand>
{
    public const int MaxContentChars = 4000;
    public const string HistoryRejected =
        "history is no longer accepted: the server keeps the transcript. Send conversationId instead.";

    public ChatAiValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxContentChars);
        // The transcript is the server's: any caller-supplied turn could forge a tool result.
        RuleFor(x => x.History)
            .Null()
            .WithMessage(HistoryRejected)
            .OverridePropertyName("history");
    }
}

public sealed class ChatAiHandler(
    AgentLoop agentLoop,
    IAgentRepository agents,
    IConversationRepository conversations,
    IUnitOfWork unitOfWork,
    IAgentRuntimeContext runtime,
    IAiUsageTracker usageTracker,
    IContentGuard contentGuard,
    AiQuota quota,
    ITenantContext tenantContext,
    ICurrentUserAccessor currentUser,
    IHostEnvironment environment,
    IOptions<LlmOptions> llmOptions,
    IOptions<ConversationOptions> conversationOptions,
    ILogger<ChatAiHandler> logger) : IRequestHandler<ChatAiCommand, ChatAiOutput>
{
    public const string AgentMismatchMessage = "This conversation is bound to another agent.";
    public const string MaxItemsMessage = "This conversation reached its item limit. Start a new conversation.";
    public const string ConcurrentAppendMessage = "The conversation changed while this turn ran. Send the message again.";

    public async Task<ChatAiOutput> Handle(ChatAiCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before using AI chat.");
        var userId = currentUser.UserId
            ?? throw new BusinessRuleException("An authenticated user is required to chat.");

        logger.LogInformation("AI chat request for tenant {TenantId}", tenantId);

        var conversation = await ResolveConversationAsync(request.ConversationId, cancellationToken);
        var agent = await ResolveAgentAsync(request.AgentId, conversation, cancellationToken);
        var options = conversationOptions.Value;
        if (conversation is not null && conversation.Items.Count >= options.MaxItems)
            throw new BusinessRuleException(MaxItemsMessage);

        runtime.Set(agent.Id);
        await quota.EnsureWithinAsync(cancellationToken);

        var started = DateTime.UtcNow;
        AgentResult? result = null;
        string? errorCode = null;

        try
        {
            await contentGuard.EnsureMessageAllowedAsync(request.Message, cancellationToken);

            result = await agentLoop.RunAsync(
                request.Message,
                agent.Instructions,
                conversation?.HistoryWindow(options.HistoryWindow),
                agent.ToolNames,
                cancellationToken,
                agent.Model);

            conversation = await PersistTurnAsync(conversation, tenantId, userId, agent.Id, request.Message, result, cancellationToken);
            return new ChatAiOutput(result.Reply, result.IterationsUsed, conversation.Id);
        }
        catch (ContentBlockedException)
        {
            logger.LogWarning("AI chat message blocked by content guard for tenant {TenantId}", tenantId);
            errorCode = AgentGuardrails.ContentBlockedErrorCode;
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentLoop failed for tenant {TenantId}", tenantId);
            errorCode = ex.GetType().Name;
            throw;
        }
        finally
        {
            logger.LogInformation(
                "AI chat finished for tenant {TenantId}, agent {AgentId}, conversation {ConversationId}, success {Success}",
                tenantId,
                agent.Id,
                conversation?.Id ?? request.ConversationId,
                errorCode is null);

            await usageTracker.TrackAsync(
                new AiUsageRecord(
                    Service: "llm",
                    Provider: LlmServiceResolver.ProviderLabel(environment, llmOptions.Value),
                    Model: agent.Model ?? llmOptions.Value.Model,
                    Module: "ai",
                    Operation: AiUsageOperations.Chat,
                    TenantId: tenantId,
                    AgentId: agent.Id,
                    InputTokens: result?.InputTokens ?? 0,
                    OutputTokens: result?.OutputTokens ?? 0,
                    Cost: result?.Cost,
                    Latency: DateTime.UtcNow - started,
                    Success: errorCode is null,
                    ErrorCode: errorCode),
                cancellationToken);
        }
    }

    /// <summary>User turn, every message the loop produced, then the reply — one SaveChanges.</summary>
    private async Task<Conversation> PersistTurnAsync(
        Conversation? conversation,
        Guid tenantId,
        Guid userId,
        Guid agentId,
        string message,
        AgentResult result,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (conversation is null)
        {
            conversation = Conversation.Create(tenantId, userId, agentId, message, now);
            await conversations.AddAsync(conversation, cancellationToken);
        }

        conversations.AddItem(conversation.Append(ConversationRoles.User, message, now));
        foreach (var turn in result.TurnMessages ?? [])
            conversations.AddItem(conversation.Append(turn.Role, turn.Content, now));
        conversations.AddItem(conversation.Append(ConversationRoles.Assistant, result.Reply, now));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            conversations.DiscardChanges();
            throw new BusinessRuleException(ConcurrentAppendMessage);
        }

        return conversation;
    }

    private async Task<Conversation?> ResolveConversationAsync(Guid? conversationId, CancellationToken cancellationToken)
    {
        if (conversationId is not { } id)
            return null;

        return await conversations.GetOwnedAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Conversation '{id}' was not found.");
    }

    private async Task<Agent> ResolveAgentAsync(Guid? agentId, Conversation? conversation, CancellationToken cancellationToken)
    {
        if (conversation is not null)
        {
            if (agentId is { } requested && requested != conversation.AgentId)
                throw new BusinessRuleException(AgentMismatchMessage);

            var bound = await agents.GetByIdAsync(conversation.AgentId, cancellationToken);
            if (bound is null || !bound.IsActive)
                throw new NotFoundException($"Agent '{conversation.AgentId}' was not found.");

            return bound;
        }

        if (agentId is { } id)
        {
            var agent = await agents.GetByIdAsync(id, cancellationToken);
            if (agent is null || !agent.IsActive)
                throw new NotFoundException($"Agent '{id}' was not found.");

            return agent;
        }

        return await agents.GetDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Default agent was not found.");
    }
}

public sealed class ChatAiEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ai/chat", async (
            ChatAiRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new ChatAiCommand(body.Message, body.History, body.AgentId, body.ConversationId),
                cancellationToken);
            return Results.Ok(new ChatAiResponse(result.ConversationId, result.Reply, result.IterationsUsed));
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ChatAi")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.Authenticated)
        .RequireRateLimiting(RateLimitPolicies.AiRateLimitPolicy)
        .Produces<ChatAiResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

public sealed record ChatAiRequest(
    string Message,
    IReadOnlyList<LlmMessage>? History = null,
    Guid? AgentId = null,
    Guid? ConversationId = null);

public sealed record ChatAiResponse(Guid ConversationId, string Reply, int IterationsUsed);
