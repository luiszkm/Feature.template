using Api.Host;
using Api.Host.Extensions;
using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record ChatAiCommand(string Message, IReadOnlyList<LlmMessage>? History = null)
    : ICommand<ChatAiOutput>;

public sealed class ChatAiValidator : AbstractValidator<ChatAiCommand>
{
    public ChatAiValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ChatAiHandler(
    AgentLoop agentLoop,
    IAiUsageTracker usageTracker,
    ITenantContext tenantContext,
    ILogger<ChatAiHandler> logger) : IRequestHandler<ChatAiCommand, ChatAiOutput>
{
    public async Task<ChatAiOutput> Handle(ChatAiCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before using AI chat.");

        logger.LogInformation("AI chat request for tenant {TenantId}", tenantId);

        var started = DateTime.UtcNow;
        AgentResult? result = null;
        string? errorCode = null;

        try
        {
            result = await agentLoop.RunAsync(
                request.Message,
                AgentSystemPrompt.Text,
                request.History,
                cancellationToken);

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
                    Provider: "stub",
                    Model: "stub",
                    Module: "ai",
                    Operation: "chat",
                    TenantId: tenantId,
                    TokensUsed: result?.TotalTokens,
                    Latency: DateTime.UtcNow - started,
                    Success: errorCode is null,
                    ErrorCode: errorCode),
                cancellationToken);
        }
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
            var result = await mediator.Send(new ChatAiCommand(body.Message, body.History), cancellationToken);
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

public sealed record ChatAiRequest(string Message, IReadOnlyList<LlmMessage>? History = null);

public sealed record ChatAiResponse(string Reply, int IterationsUsed);
