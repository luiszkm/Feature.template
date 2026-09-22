using Api.Shared;
using MediatR;

namespace Api.Features.Ai;

public sealed record ListModelsQuery : IQuery<IReadOnlyList<ModelOutput>>;

public sealed class ListModelsHandler(IModelCatalog catalog)
    : IRequestHandler<ListModelsQuery, IReadOnlyList<ModelOutput>>
{
    public Task<IReadOnlyList<ModelOutput>> Handle(ListModelsQuery request, CancellationToken cancellationToken) =>
        catalog.ListAsync(cancellationToken);
}

public sealed class ListModelsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ai/models", async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new ListModelsQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("ListModels")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsRead)
        .Produces<IReadOnlyList<ModelOutput>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
