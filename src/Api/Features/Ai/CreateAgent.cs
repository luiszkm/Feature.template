using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record CreateAgentCommand(
    string Name,
    string Instructions,
    IReadOnlyList<string> ToolNames) : ICommand<AgentOutput>;

public sealed class CreateAgentValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentValidator(ToolRegistry tools)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Instructions).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ToolNames).NotNull();
        RuleForEach(x => x.ToolNames)
            .Must(tools.IsKnown)
            .WithMessage("Unknown tool '{PropertyValue}'.");
    }
}

public sealed class CreateAgentHandler(
    IAgentRepository agents,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateAgentCommand, AgentOutput>
{
    public async Task<AgentOutput> Handle(CreateAgentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before managing agents.");

        var existing = await agents.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
            throw new BusinessRuleException($"Agent with name '{request.Name}' already exists.");

        var agent = Agent.Create(tenantId, request.Name, request.Instructions, request.ToolNames);
        await agents.AddAsync(agent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AgentMapper.ToOutput(agent);
    }
}

public sealed class CreateAgentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ai/agents", async (
            CreateAgentCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/ai/agents/{result.AgentId}", result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("CreateAgent")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces<AgentOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
