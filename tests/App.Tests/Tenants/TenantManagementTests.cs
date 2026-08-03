using App.Features.Tenants;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Tenants;

public sealed class UpdateTenantTests
{
    [Fact]
    public void Validator_ShouldFail_WhenDisplayNameIsEmpty()
    {
        var validator = new UpdateTenantValidator();
        var command = new UpdateTenantCommand(Guid.NewGuid(), "", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTenant_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldUpdateTenant_WhenInputIsValid));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpdateTenantHandler>();

        var result = await handler.Handle(
            new UpdateTenantCommand(
                WellKnownTenants.Development,
                "Updated Dev Tenant",
                "dev@example.com"),
            CancellationToken.None);

        Assert.Equal(WellKnownTenants.Development, result.TenantId);
        Assert.Equal("Updated Dev Tenant", result.DisplayName);
        Assert.Equal("dev@example.com", result.ContactEmail);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldThrow_WhenTenantDoesNotExist));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpdateTenantHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new UpdateTenantCommand(Guid.NewGuid(), "Name", null),
                CancellationToken.None));
    }
}

public sealed class DeactivateTenantTests
{
    [Fact]
    public async Task Handle_ShouldDeactivateTenant_WhenTenantExists()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldDeactivateTenant_WhenTenantExists));

        using var scope = provider.CreateScope();
        var deactivateHandler = scope.ServiceProvider.GetRequiredService<DeactivateTenantHandler>();
        var getHandler = scope.ServiceProvider.GetRequiredService<GetTenantHandler>();

        await deactivateHandler.Handle(
            new DeactivateTenantCommand(WellKnownTenants.Development),
            CancellationToken.None);

        var result = await getHandler.Handle(
            new GetTenantQuery(WellKnownTenants.Development),
            CancellationToken.None);

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithTenants(nameof(Handle_ShouldThrow_WhenTenantDoesNotExist));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateTenantHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeactivateTenantCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
