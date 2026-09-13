using Api.Features.Identity;
using Api.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Identity;

public sealed class LoginTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    #region Validator

    [Fact]
    public void Validator_ShouldFail_WhenEmailIsEmpty()
    {
        var validator = new LoginValidator();
        var command = UserBuilder.ValidLoginCommand() with { Email = "" };

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validator_ShouldPass_WhenInputIsValid()
    {
        var validator = new LoginValidator();
        var result = validator.TestValidate(UserBuilder.ValidLoginCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Handler

    [Fact]
    public async Task Handle_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        var provider = TestServiceFactory.CreateWithLogin(nameof(Handle_ShouldReturnTokens_WhenCredentialsAreValid));
        await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        var result = await handler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("user@example.com", result.User.Email);
        Assert.NotNull(result.User.LastLoginAt);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenPasswordIsInvalid()
    {
        var provider = TestServiceFactory.CreateWithLogin(nameof(Handle_ShouldThrowUnauthorized_WhenPasswordIsInvalid));
        await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(UserBuilder.ValidLoginCommand() with { Password = "WrongPass1!" }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithLogin(nameof(Handle_ShouldThrowUnauthorized_WhenUserDoesNotExist));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None));
    }

    #endregion
}
