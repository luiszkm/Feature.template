using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record DeactivateAgentCommand(Guid AgentId) : ICommand<bool>;

public sealed class DeactivateAgentValidator : AbstractValidator<DeactivateAgentCommand>
{
    public DeactivateAgentValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty();
    }
}

public sealed class DeactivateAgentHandler(
    IAgentRepository agents,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateAgentCommand, bool>
{
    public async Task<bool> Handle(DeactivateAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        if (agent.IsActive)
        {
            var active = await agents.CountActiveAsync(cancellationToken);
            if (active <= 1)
                throw new BusinessRuleException("Cannot deactivate the last active agent in the tenant.");
        }

        agent.Deactivate();
        await agents.UpdateAsync(agent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class DeactivateAgentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/ai/agents/{agentId:guid}", async (
            Guid agentId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeactivateAgentCommand(agentId), cancellationToken);
            return Results.NoContent();
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("DeactivateAgent")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
