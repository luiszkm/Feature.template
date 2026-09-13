using MediatR;

namespace Api.Shared;

public sealed class TenantContextBehavior<TRequest, TResponse>(ITenantContext tenantContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is ITenantExemptRequest)
            return await next(cancellationToken);

        if (tenantContext.TenantId is null)
            throw new BusinessRuleException("Tenant must be resolved before executing this request.");

        return await next(cancellationToken);
    }
}
