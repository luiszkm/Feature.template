using Api.Shared;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Host.Extensions;

public static class ExceptionHandlerExtensions
{
    public static WebApplication UseExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var exception = context.Features
                    .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
                context.Response.ContentType = "application/problem+json";

                switch (exception)
                {
                    case BusinessRuleException business:
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Business rule violation",
                            Detail = business.Message,
                            Status = StatusCodes.Status409Conflict
                        });
                        break;
                    case ValidationException validation:
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(
                            validation.Errors.GroupBy(e => e.PropertyName)
                                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
                        {
                            Title = "Validation failed",
                            Status = StatusCodes.Status400BadRequest
                        });
                        break;
                    case UnauthorizedAccessException unauthorized:
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Unauthorized",
                            Detail = unauthorized.Message,
                            Status = StatusCodes.Status401Unauthorized
                        });
                        break;
                    case NotFoundException notFound:
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Not found",
                            Detail = notFound.Message,
                            Status = StatusCodes.Status404NotFound
                        });
                        break;
                    case ServiceUnavailableException unavailable:
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Service unavailable",
                            Detail = unavailable.Message,
                            Status = StatusCodes.Status503ServiceUnavailable
                        });
                        break;
                    default:
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Unexpected error",
                            Status = StatusCodes.Status500InternalServerError
                        });
                        break;
                }
            });
        });

        return app;
    }
}
