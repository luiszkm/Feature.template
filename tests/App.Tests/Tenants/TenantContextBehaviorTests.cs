using App.Features.Tenants;
using App.Shared;
using App.Tests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Tenants;

public sealed class TenantContextBehaviorTests
{
    [Fact]
    public async Task CreateTenant_ShouldNotRequireTenantContext()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(CreateTenant_ShouldNotRequireTenantContext));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();

        var result = await handler.Handle(
            new CreateTenantCommand("acme", "Acme Corp", null, TenantIsolationMode.SharedDb),
            CancellationToken.None);

        Assert.Equal("acme", result.TenantKey);
    }

    [Fact]
    public async Task ListTenants_ShouldNotRequireTenantContext()
    {
        var tenantContext = new TenantContext();
        var behavior = new TenantContextBehavior<ListTenantsQuery, PaginatedListOutput<TenantOutput>>(tenantContext);

        var result = await behavior.Handle(
            new ListTenantsQuery(),
            _ => Task.FromResult(new PaginatedListOutput<TenantOutput>(1, 20, 0, [])),
            CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
    }
}
