using Api.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed record ChatAiCommand(
    string Message,
    IReadOnlyList<LlmMessage>? History = null,
    Guid? AgentId = null) : ICommand<ChatAiOutput>;

public sealed class ChatAiValidator : AbstractValidator<ChatAiCommand>
{
    public ChatAiValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxContentChars);
        RuleFor(x => x.History)
            .Must(history => history!.Count <= MaxHistoryItems)
            .WithMessage($"History accepts at most {MaxHistoryItems} items.")
            .When(x => x.History is not null);
        // Only text turns the browser itself produces: a caller-supplied `tool` or `system`
        // message would reach the model as a tool result or instruction that never happened.
        RuleForEach(x => x.History).NotNull().ChildRules(item =>
        {
            item.RuleFor(m => m.Role)
                .Must(role => AcceptedRoles.Contains(role ?? string.Empty))
                .WithMessage("History role must be 'user' or 'assistant'.");
            item.RuleFor(m => m.Content).NotNull().MaximumLength(MaxContentChars);
            item.RuleFor(m => m.ToolCalls)
                .Must(calls => calls is null || calls.Count == 0)
                .WithMessage("History items cannot carry tool calls.");
            item.RuleFor(m => m.ToolCallId)
                .Null()
                .WithMessage("History items cannot carry a tool call id.");
        });
    }

    public const int MaxHistoryItems = 50;
    public const int MaxContentChars = 4000;

    private static readonly HashSet<string> AcceptedRoles = new(StringComparer.OrdinalIgnoreCase) { "user", "assistant" };
}

public sealed class ChatAiHandler(
    AgentLoop agentLoop,
    IAgentRepository agents,
    IAgentRuntimeContext runtime,
    IAiUsageTracker usageTracker,
    IContentGuard contentGuard,
    AiQuota quota,
    ITenantContext tenantContext,
    IHostEnvironment environment,
    IOptions<LlmOptions> llmOptions,
    ILogger<ChatAiHandler> logger) : IRequestHandler<ChatAiCommand, ChatAiOutput>
{
    public async Task<ChatAiOutput> Handle(ChatAiCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before using AI chat.");

        logger.LogInformation("AI chat request for tenant {TenantId}", tenantId);

        var agent = await ResolveAgentAsync(request.AgentId, cancellationToken);
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
                request.History,
                agent.ToolNames,
                cancellationToken,
                agent.Model);

            return new ChatAiOutput(result.Reply, result.IterationsUsed);
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

    private async Task<Agent> ResolveAgentAsync(Guid? agentId, CancellationToken cancellationToken)
    {
        if (agentId is { } id)
        {
            var agent = await agents.GetByIdAsync(id, cancellationToken);
            if (agent is null || !agent.IsActive)
                throw new NotFoundException($"Agent '{id}' was not found.");

            return agent;
        }

        var seed = await agents.GetDefaultAsync(cancellationToken);
        if (seed is null)
            throw new NotFoundException("Default agent was not found.");

        return seed;
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
                new ChatAiCommand(body.Message, body.History, body.AgentId),
                cancellationToken);
            return Results.Ok(new ChatAiResponse(result.Reply, result.IterationsUsed));
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
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}

public sealed record ChatAiRequest(
    string Message,
    IReadOnlyList<LlmMessage>? History = null,
    Guid? AgentId = null);

public sealed record ChatAiResponse(string Reply, int IterationsUsed);
