using App.Shared;
using FluentValidation;
using MediatR;

namespace App.Features.Identity;

public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ICommand<bool>;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
    }
}

public sealed class ConfirmEmailHandler(
    IUserRepository userRepository,
    IEmailConfirmationTokenService emailConfirmationTokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmEmailCommand, bool>
{
    public async Task<bool> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        if (!emailConfirmationTokenService.ValidateToken(user.Id, user.SecurityStamp, request.Token))
            throw new UnauthorizedAccessException("Invalid email confirmation token.");

        if (user.EmailConfirmed)
            return true;

        user.ConfirmEmail();
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed class ConfirmEmailEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/identity/users/{userId:guid}/confirm-email", async (
            Guid userId,
            ConfirmEmailRequest body,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new ConfirmEmailCommand(userId, body.Token), cancellationToken);
            return Results.NoContent();
        })
        .WithName("ConfirmEmail")
        .WithTags("Identity")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record ConfirmEmailRequest(string Token);
