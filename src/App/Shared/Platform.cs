using System.Reflection;
using FluentValidation;
using MediatR;

namespace App.Shared;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantKey { get; }
}

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? TenantKey { get; private set; }

    public void SetTenant(Guid tenantId, string? tenantKey = null)
    {
        TenantId = tenantId;
        TenantKey = tenantKey;
    }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var validationResults = new List<FluentValidation.Results.ValidationResult>();
        foreach (var validator in validators)
            validationResults.Add(await validator.ValidateAsync(context, cancellationToken));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}

public static class PlatformExtensions
{
    public static IServiceCollection AddPlatform(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantContextBehavior<,>));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        return services;
    }

    public static WebApplication MapEndpointsFromAssembly(this WebApplication app)
    {
        var endpointTypes = app.Services.GetRequiredService<IEnumerable<IEndpoint>>().ToList();
        foreach (var endpoint in endpointTypes)
            endpoint.Map(app);

        return app;
    }
}

public static class EndpointRegistration
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        var endpointTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpoint).IsAssignableFrom(t));

        foreach (var type in endpointTypes)
            services.AddSingleton(typeof(IEndpoint), type);

        return services;
    }
}
