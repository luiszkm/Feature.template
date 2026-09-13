using App.Host.Security;
using App.Shared;
using MediatR;

namespace App.Features.Identity;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserOutput>;

public sealed class GetUserHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserQuery, UserOutput>
{
    public async Task<UserOutput> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        return user.ToOutput();
    }
}

public sealed class GetUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/identity/users/{userId:guid}", async (
            Guid userId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetUserQuery(userId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUser")
        .WithTags("Identity")
        .RequireAuthorization(SecurityPolicies.UserReadOrSelf)
        .Produces<UserOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
