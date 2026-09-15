using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Ai;

public sealed record DeleteAgentFileCommand(Guid AgentId, Guid FileId) : ICommand<bool>;

public sealed class DeleteAgentFileValidator : AbstractValidator<DeleteAgentFileCommand>
{
    public DeleteAgentFileValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
    }
}

public sealed class DeleteAgentFileHandler(
    IAgentRepository agents,
    IAgentFileRepository files,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteAgentFileCommand, bool>
{
    public async Task<bool> Handle(DeleteAgentFileCommand request, CancellationToken cancellationToken)
    {
        _ = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw new NotFoundException($"Agent '{request.AgentId}' was not found.");

        var file = await files.GetByIdAsync(request.FileId, cancellationToken);
        if (file is null || file.AgentId != request.AgentId)
            throw new NotFoundException($"Agent file '{request.FileId}' was not found.");

        await files.DeleteAsync(file, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class DeleteAgentFileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/ai/agents/{agentId:guid}/files/{fileId:guid}", async (
            Guid agentId,
            Guid fileId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeleteAgentFileCommand(agentId, fileId), cancellationToken);
            return Results.NoContent();
        })
        .RequireFeature(FeatureFlags.EnableAI)
        .WithName("DeleteAgentFile")
        .WithTags("Ai")
        .RequireAuthorization(SecurityPolicies.AiAgentsManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
