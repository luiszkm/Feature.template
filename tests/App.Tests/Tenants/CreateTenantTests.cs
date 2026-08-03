using App.Features.Tenants;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Tenants;

public sealed class CreateTenantTests
{
    [Fact]
    public void Validator_ShouldFail_WhenTenantKeyIsEmpty()
    {
        var validator = new CreateTenantValidator();
        var command = new CreateTenantCommand("", "Name", null, TenantIsolationMode.SharedDb);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantKey);
    }

    [Fact]
    public async Task Handle_ShouldCreateTenant_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldCreateTenant_WhenInputIsValid));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();

        var result = await handler.Handle(
            new CreateTenantCommand("acme", "Acme Corp", "ops@acme.com", TenantIsolationMode.SharedDb),
            CancellationToken.None);

        Assert.Equal("acme", result.TenantKey);
        Assert.Equal("Acme Corp", result.DisplayName);
        Assert.NotEqual(Guid.Empty, result.TenantId);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantKeyAlreadyExists()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldThrow_WhenTenantKeyAlreadyExists));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();
        var command = new CreateTenantCommand("acme", "Acme Corp", null, TenantIsolationMode.SharedDb);

        await handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_ShouldFail_WhenIsolationModeIsNotSharedDb()
    {
        var validator = new CreateTenantValidator();
        var command = new CreateTenantCommand(
            "acme",
            "Acme Corp",
            null,
            TenantIsolationMode.SchemaPerTenant);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.IsolationMode);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRule_WhenIsolationModeIsDedicatedDb()
    {
        var provider = TestServiceFactory.CreateWithTenants(
            nameof(Handle_ShouldThrowBusinessRule_WhenIsolationModeIsDedicatedDb));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(
                new CreateTenantCommand("solo", "Solo", null, TenantIsolationMode.DedicatedDb),
                CancellationToken.None));
    }
}

public sealed class ListTenantsTests
{
    [Fact]
    public async Task Handle_ShouldReturnSeededTenants()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldReturnSeededTenants));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, WellKnownTenants.Development);
        var handler = scope.ServiceProvider.GetRequiredService<ListTenantsHandler>();

        var result = await handler.Handle(new ListTenantsQuery(), CancellationToken.None);

        Assert.True(result.TotalCount >= 2);
        Assert.Contains(result.Data, t => t.TenantKey == "public");
        Assert.Contains(result.Data, t => t.TenantKey == "dev");
    }

    [Fact]
    public void Query_ShouldImplement_ITenantExemptRequest()
    {
        Assert.IsAssignableFrom<ITenantExemptRequest>(new ListTenantsQuery());
    }
}

public sealed class GetTenantTests
{
    [Fact]
    public async Task Handle_ShouldReturnTenant_WhenIdExists()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldReturnTenant_WhenIdExists));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetTenantHandler>();

        var result = await handler.Handle(
            new GetTenantQuery(WellKnownTenants.Public),
            CancellationToken.None);

        Assert.Equal("public", result.TenantKey);
        Assert.Equal(WellKnownTenants.Public, result.TenantId);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldThrow_WhenTenantDoesNotExist));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetTenantHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetTenantQuery(Guid.NewGuid()), CancellationToken.None));
    }
}

public sealed class TenantStoreTests
{
    [Fact]
    public async Task GetByKeyAsync_ShouldResolveDevTenant()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(GetByKeyAsync_ShouldResolveDevTenant));

        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITenantStore>();

        var tenant = await store.GetByKeyAsync("dev", CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal(WellKnownTenants.Development, tenant!.Id);
        Assert.True(tenant.IsActive);
    }
}
