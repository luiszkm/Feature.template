using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record CreateAgentFileCommand(Guid AgentId, string Name, string Content)
    : ICommand<AgentFileOutput>;

public sealed class CreateAgentFileValidator : AbstractValidator<CreateAgentFileCommand>
{
    public CreateAgentFileValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotNull().MaximumLength(100_000);
    }
}

public sealed class CreateAgentFileHandler(
    IAgentRepository agents,
    IAgentFileRepository files,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateAgentFileCommand, AgentFileOutput>
{
    public async Task<AgentFileOutput> Handle(
        CreateAgentFileCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before managing agent files.");

        var agent = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        var file = AgentFile.Create(tenantId, agent.Id, request.Name, request.Content);
        await files.AddAsync(file, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AgentMapper.ToOutput(file, includeContent: false);
    }
}

public sealed class CreateAgentFileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/ai/agents/{agentId:guid}/files", async (
            Guid agentId,
            CreateAgentFileRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new CreateAgentFileCommand(agentId, body.Name, body.Content),
                cancellationToken);
            return Results.Created(
                $"/api/v1/ai/agents/{agentId}/files/{result.FileId}",
                result);
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("CreateAgentFile")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces<AgentFileOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CreateAgentFileRequest(string Name, string Content);
