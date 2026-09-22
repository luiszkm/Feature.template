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
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ChatAiHandler(
    AgentLoop agentLoop,
    IAgentRepository agents,
    IAgentRuntimeContext runtime,
    IAiUsageTracker usageTracker,
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

        var started = DateTime.UtcNow;
        AgentResult? result = null;
        string? errorCode = null;

        try
        {
            result = await agentLoop.RunAsync(
                request.Message,
                agent.Instructions,
                request.History,
                agent.ToolNames,
                cancellationToken,
                agent.Model);

            return new ChatAiOutput(result.Reply, result.IterationsUsed);
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
        .Produces<ChatAiResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record ChatAiRequest(
    string Message,
    IReadOnlyList<LlmMessage>? History = null,
    Guid? AgentId = null);

public sealed record ChatAiResponse(string Reply, int IterationsUsed);
