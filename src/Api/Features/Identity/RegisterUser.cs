using Api.Shared;
using FluentValidation;
using MediatR;

namespace Api.Features.Identity;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : ICommand<UserOutput>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.FirstName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MinimumLength(2).MaximumLength(100);
    }
}

public sealed class RegisterUserHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IPasswordHasher passwordHasher) : IRequestHandler<RegisterUserCommand, UserOutput>
{
    public async Task<UserOutput> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new BusinessRuleException("Tenant must be resolved before registering a user.");

        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new BusinessRuleException("Email is already in use.");

        var user = User.Create(
            tenantId,
            Email.Create(request.Email),
            passwordHasher.Hash(request.Password),
            request.FirstName,
            request.LastName);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return user.ToOutput();
    }
}

public sealed class RegisterUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/identity/register", async (
            RegisterUserCommand command,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Created($"/api/v1/identity/users/{result.Id}", result);
        })
        .WithName("RegisterUser")
        .WithTags("Identity")
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.AuthRateLimitPolicy)
        .Produces<UserOutput>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
