using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record UpdateAgentCommand(
    Guid AgentId,
    string Name,
    string Instructions,
    IReadOnlyList<string> ToolNames,
    string? Model = null) : ICommand<AgentOutput>;

public sealed class UpdateAgentValidator : AbstractValidator<UpdateAgentCommand>
{
    public UpdateAgentValidator(ToolRegistry tools, IModelCatalog catalog)
    {
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Instructions).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ToolNames).NotNull();
        RuleForEach(x => x.ToolNames)
            .Must(tools.IsKnown)
            .WithMessage("Unknown tool '{PropertyValue}'.");
        RuleFor(x => x.Model)
            .MaximumLength(200)
            .MustAsync(async (model, cancellationToken) =>
                string.IsNullOrWhiteSpace(model) || await catalog.ContainsAsync(model.Trim(), cancellationToken))
            .WithMessage("Model '{PropertyValue}' is not in the model catalog.")
            .When(x => x.Model is not null);
    }
}

public sealed class UpdateAgentHandler(
    IAgentRepository agents,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateAgentCommand, AgentOutput>
{
    public async Task<AgentOutput> Handle(UpdateAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        var clash = await agents.GetByNameAsync(request.Name, cancellationToken);
        if (clash is not null && clash.Id != agent.Id)
            throw new BusinessRuleException($"Agent with name '{request.Name}' already exists.");

        agent.Update(request.Name, request.Instructions, request.ToolNames, request.Model);
        await agents.UpdateAsync(agent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AgentMapper.ToOutput(agent);
    }
}

public sealed class UpdateAgentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/ai/agents/{agentId:guid}", async (
            Guid agentId,
            UpdateAgentRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new UpdateAgentCommand(agentId, body.Name, body.Instructions, body.ToolNames, body.Model),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("UpdateAgent")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces<AgentOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}

public sealed record UpdateAgentRequest(
    string Name,
    string Instructions,
    IReadOnlyList<string> ToolNames,
    string? Model = null);
