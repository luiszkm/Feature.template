using App.Host.Security;
using App.Shared;
using MediatR;

namespace App.Features.Identity;

public sealed record DeleteUserCommand(Guid UserId) : ICommand<bool>;

public sealed class DeleteUserHandler(
    IUserRepository userRepository,
    ISecurityStampService securityStampService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteUserCommand, bool>
{
    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        user.Deactivate();
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await securityStampService.RegenerateAsync(user.TenantId, user.Id, cancellationToken);

        return true;
    }
}

public sealed class DeleteUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/identity/users/{userId:guid}", async (
            Guid userId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeleteUserCommand(userId), cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .WithTags("Identity")
        .RequireAuthorization(SecurityPolicies.UsersManage)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
