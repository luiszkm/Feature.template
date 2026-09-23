using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record GetAiUsageQuery(
    DateTime? From = null,
    DateTime? To = null,
    int PageNumber = 1,
    int PageSize = 20)
    : ListQuery(PageNumber, PageSize), IQuery<PaginatedListOutput<AiUsageOutput>>;

public sealed record AiUsageOutput(
    Guid AgentId,
    string AgentName,
    int Calls,
    int Failures,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    DateTime LastUsedAt);

public sealed class GetAiUsageValidator : ListQueryValidator<GetAiUsageQuery>
{
    public GetAiUsageValidator()
    {
        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To!.Value)
            .WithMessage("'from' must not be after 'to'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public sealed class GetAiUsageHandler(IAiUsageRepository usage, IAgentRepository agents)
    : IRequestHandler<GetAiUsageQuery, PaginatedListOutput<AiUsageOutput>>
{
    public async Task<PaginatedListOutput<AiUsageOutput>> Handle(GetAiUsageQuery request, CancellationToken cancellationToken)
    {
        var page = await usage.SummarizeByAgentAsync(request.From, request.To, request, cancellationToken);

        var rows = new List<AiUsageOutput>(page.Data.Count);
        foreach (var summary in page.Data)
        {
            // Deactivated agents keep their name; the ledger has no FK, so a row may outlive its agent.
            var agent = await agents.GetByIdAsync(summary.AgentId, cancellationToken);
            rows.Add(new AiUsageOutput(
                summary.AgentId,
                agent?.Name ?? summary.AgentId.ToString(),
                summary.Calls,
                summary.Failures,
                summary.InputTokens,
                summary.OutputTokens,
                summary.TotalTokens,
                summary.LastUsedAt));
        }

        return new PaginatedListOutput<AiUsageOutput>(page.PageNumber, page.PageSize, page.TotalCount, rows);
    }
}

public sealed class GetAiUsageEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/usage", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? pageNumber,
            int? pageSize,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new GetAiUsageQuery(from?.UtcDateTime, to?.UtcDateTime, pageNumber ?? 1, pageSize ?? 20),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("GetAiUsage")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<PaginatedListOutput<AiUsageOutput>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
