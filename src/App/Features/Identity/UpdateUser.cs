using App.Host.Security;
using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Identity;

public sealed record UpdateUserCommand(Guid UserId, string FirstName, string LastName) : ICommand<UserOutput>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MinimumLength(2).MaximumLength(100);
    }
}

public sealed class UpdateUserHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateUserCommand, UserOutput>
{
    public async Task<UserOutput> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        user.UpdateProfile(request.FirstName, request.LastName);
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return user.ToOutput();
    }
}

public sealed class UpdateUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/identity/users/{userId:guid}", async (
            Guid userId,
            UpdateUserRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(
                new UpdateUserCommand(userId, body.FirstName, body.LastName),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateUser")
        .WithTags("Identity")
        .RequireAuthorization(SecurityPolicies.UserManageOrSelf)
        .Produces<UserOutput>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateUserRequest(string FirstName, string LastName);
