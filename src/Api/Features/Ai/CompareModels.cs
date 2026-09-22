using System.Diagnostics;
using System.Text;
using Api.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed record CompareModelsCommand(
    Guid AgentId,
    string Prompt,
    IReadOnlyList<string> Models,
    IReadOnlyList<ComparisonAttachmentInput>? Attachments = null) : ICommand<ComparisonOutput>;

public sealed record ComparisonAttachmentInput(string Name, string Content);

public sealed class CompareModelsValidator : AbstractValidator<CompareModelsCommand>
{
    public const int MinModels = 2;
    public const int MaxModels = 4;
    public const int MaxAttachments = 3;
    public const int MaxAttachmentChars = 100_000;

    public CompareModelsValidator(IModelCatalog catalog)
    {
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Models)
            .NotNull()
            .Must(models => models.Count is >= MinModels and <= MaxModels)
            .WithMessage($"Choose between {MinModels} and {MaxModels} models.")
            .Must(models => models.Distinct(StringComparer.Ordinal).Count() == models.Count)
            .WithMessage("Models must be distinct.");
        RuleForEach(x => x.Models)
            .MustAsync(async (model, cancellationToken) =>
                !string.IsNullOrWhiteSpace(model) && await catalog.ContainsAsync(model, cancellationToken))
            .WithMessage("Model '{PropertyValue}' is not in the model catalog.");
        RuleFor(x => x.Attachments)
            .Must(items => items!.Count <= MaxAttachments)
            .WithMessage($"At most {MaxAttachments} attachments.")
            .Must(items => items!.Sum(item => item.Content?.Length ?? 0) <= MaxAttachmentChars)
            .WithMessage($"Attachments exceed {MaxAttachmentChars} characters in total.")
            .When(x => x.Attachments is not null);
        RuleForEach(x => x.Attachments).ChildRules(item =>
        {
            item.RuleFor(a => a.Name).NotEmpty().MaximumLength(200);
            item.RuleFor(a => a.Content).NotNull();
        });
    }
}

public sealed class CompareModelsHandler(
    ILlmService llm,
    IAgentRepository agents,
    IModelComparisonRepository comparisons,
    IServiceScopeFactory scopeFactory,
    IContentGuard contentGuard,
    AiQuota quota,
    ITenantContext tenantContext,
    ICurrentUserAccessor currentUser,
    IUnitOfWork unitOfWork,
    IHostEnvironment environment,
    IOptions<LlmOptions> llmOptions,
    ILogger<CompareModelsHandler> logger) : IRequestHandler<CompareModelsCommand, ComparisonOutput>
{
    public const string OpenRouterRequiredMessage = "Comparação requer o provider OpenRouter";
    public const string TimeoutErrorCode = "Timeout";

    public async Task<ComparisonOutput> Handle(CompareModelsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before comparing models.");

        // The Agent Framework client is bound to one model and reports no cost, so it cannot compare.
        if (llm is MicrosoftAgentFrameworkLlmService)
            throw new BusinessRuleException(OpenRouterRequiredMessage);

        var agent = await agents.GetByIdAsync(request.AgentId, cancellationToken);
        if (agent is null || !agent.IsActive)
            throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        var attachments = (request.Attachments ?? [])
            .Select(item => new ComparisonAttachment(item.Name.Trim(), item.Content))
            .ToList();
        var userPrompt = BuildUserPrompt(request.Prompt, attachments);

        await quota.EnsureWithinAsync(cancellationToken);
        await contentGuard.EnsureMessageAllowedAsync(userPrompt, cancellationToken);

        // Runs are deliberately detached from the caller's token: a closed tab must not waste
        // the tokens already paid for, and the result stays visible in the history.
        var results = await Task.WhenAll(request.Models.Select((model, position) =>
            RunModelAsync(tenantId, agent, model, position, userPrompt)));

        var comparison = ModelComparison.Create(
            tenantId,
            agent.Id,
            request.Prompt,
            attachments,
            currentUser.UserId,
            results);

        await comparisons.AddAsync(comparison, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        return ComparisonMapper.ToOutput(comparison, agent.Name);
    }

    internal static string BuildUserPrompt(string prompt, IReadOnlyList<ComparisonAttachment> attachments)
    {
        var builder = new StringBuilder(prompt);
        foreach (var attachment in attachments)
            builder.Append("\n\n--- ").Append(attachment.Name).Append(" ---\n").Append(attachment.Content);
        return builder.ToString();
    }

    private async Task<ModelComparisonResult> RunModelAsync(
        Guid tenantId,
        Agent agent,
        string model,
        int position,
        string userPrompt)
    {
        // One DI scope per model: tools use AppDbContext, which does not allow concurrent use.
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (services.GetRequiredService<ITenantContext>() is TenantContext scopedTenant)
            scopedTenant.SetTenant(tenantId, tenantContext.TenantKey);
        services.GetRequiredService<IAgentRuntimeContext>().Set(agent.Id);

        var loop = services.GetRequiredService<AgentLoop>();
        var tracker = services.GetRequiredService<IAiUsageTracker>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(llmOptions.Value.CompareTimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        ModelComparisonResult result;
        AgentResult? agentResult = null;
        try
        {
            agentResult = await loop.RunAsync(
                userPrompt,
                agent.Instructions,
                history: null,
                agent.ToolNames,
                timeout.Token,
                model);
            result = ModelComparisonResult.Succeeded(position, model, agentResult, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Model {Model} timed out in comparison for agent {AgentId}", model, agent.Id);
            result = ModelComparisonResult.Unsuccessful(
                position, model, ModelComparisonStatus.TimedOut, TimeoutErrorCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Model {Model} failed in comparison for agent {AgentId}", model, agent.Id);
            result = ModelComparisonResult.Unsuccessful(
                position, model, ModelComparisonStatus.Failed, ex.GetType().Name, stopwatch.ElapsedMilliseconds);
        }

        await tracker.TrackAsync(
            new AiUsageRecord(
                Service: "llm",
                Provider: LlmServiceResolver.ProviderLabel(environment, llmOptions.Value),
                Model: model,
                Module: "ai",
                Operation: AiUsageOperations.Compare,
                TenantId: tenantId,
                AgentId: agent.Id,
                InputTokens: agentResult?.InputTokens ?? 0,
                OutputTokens: agentResult?.OutputTokens ?? 0,
                Cost: agentResult?.Cost,
                Latency: stopwatch.Elapsed,
                Success: result.Status == ModelComparisonStatus.Succeeded,
                ErrorCode: result.ErrorCode),
            CancellationToken.None);

        return result;
    }
}

public sealed class CompareModelsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ai/comparisons", async (
            CompareModelsCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/ai/comparisons/{result.ComparisonId}", result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("CompareModels")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .RequireRateLimiting(RateLimitPolicies.AiRateLimitPolicy)
        .Produces<ComparisonOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}

public sealed record ComparisonOutput(
    Guid ComparisonId,
    Guid AgentId,
    string AgentName,
    string Prompt,
    IReadOnlyList<ComparisonAttachmentOutput> Attachments,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    decimal? TotalCost,
    IReadOnlyList<ComparisonResultOutput> Results);

public sealed record ComparisonAttachmentOutput(string Name);

public sealed record ComparisonResultOutput(
    string Model,
    string Status,
    string? Reply,
    int InputTokens,
    int OutputTokens,
    decimal? Cost,
    long LatencyMs,
    int IterationsUsed,
    string? ErrorCode);

public sealed record ComparisonSummaryOutput(
    Guid ComparisonId,
    Guid AgentId,
    string AgentName,
    string PromptPreview,
    IReadOnlyList<string> Models,
    decimal? TotalCost,
    DateTime CreatedAt);

public static class ComparisonMapper
{
    public const int PromptPreviewLength = 200;

    public static ComparisonOutput ToOutput(ModelComparison comparison, string agentName) =>
        new(
            comparison.Id,
            comparison.AgentId,
            agentName,
            comparison.Prompt,
            comparison.Attachments.Select(a => new ComparisonAttachmentOutput(a.Name)).ToList(),
            comparison.CreatedAt,
            comparison.CreatedByUserId,
            comparison.TotalCost,
            comparison.Results
                .OrderBy(r => r.Position)
                .Select(r => new ComparisonResultOutput(
                    r.Model,
                    r.Status.ToString(),
                    r.Reply,
                    r.InputTokens,
                    r.OutputTokens,
                    r.Cost,
                    r.LatencyMs,
                    r.IterationsUsed,
                    r.ErrorCode))
                .ToList());

    public static ComparisonSummaryOutput ToSummary(ModelComparison comparison, string agentName) =>
        new(
            comparison.Id,
            comparison.AgentId,
            agentName,
            comparison.Prompt.Length <= PromptPreviewLength
                ? comparison.Prompt
                : comparison.Prompt[..PromptPreviewLength],
            comparison.Results.OrderBy(r => r.Position).Select(r => r.Model).ToList(),
            comparison.TotalCost,
            comparison.CreatedAt);
}
