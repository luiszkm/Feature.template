using App.Features.Identity;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Identity;

public sealed class RegisterUserTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    #region Validator

    [Fact]
    public void Validator_ShouldFail_WhenEmailIsEmpty()
    {
        var validator = new RegisterUserValidator();
        var command = UserBuilder.ValidCommand() with { Email = "" };

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validator_ShouldPass_WhenInputIsValid()
    {
        var validator = new RegisterUserValidator();
        var result = validator.TestValidate(UserBuilder.ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Handler

    [Fact]
    public async Task Handle_ShouldCreateUser_WhenInputIsValid()
    {
        var provider = TestServiceFactory.Create(nameof(Handle_ShouldCreateUser_WhenInputIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        var command = UserBuilder.ValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("user@example.com", result.Email);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenEmailAlreadyExists()
    {
        var provider = TestServiceFactory.Create(nameof(Handle_ShouldThrow_WhenEmailAlreadyExists));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        var command = UserBuilder.ValidCommand();

        await handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantIsMissing()
    {
        var provider = TestServiceFactory.Create(nameof(Handle_ShouldThrow_WhenTenantIsMissing));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(UserBuilder.ValidCommand(), CancellationToken.None));
    }

    #endregion
}
