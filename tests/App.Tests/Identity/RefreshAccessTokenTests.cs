using App.Features.Identity;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Identity;

public sealed class RefreshAccessTokenTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    #region Validator

    [Fact]
    public void Validator_ShouldFail_WhenRefreshTokenIsEmpty()
    {
        var validator = new RefreshTokenValidator();
        var command = new RefreshTokenCommand("");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Validator_ShouldPass_WhenInputIsValid()
    {
        var validator = new RefreshTokenValidator();
        var result = validator.TestValidate(new RefreshTokenCommand("valid-token"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Handler

    [Fact]
    public async Task Handle_ShouldRotateTokens_WhenRefreshTokenIsValid()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Handle_ShouldRotateTokens_WhenRefreshTokenIsValid));
        await TestServiceFactory.SeedConfirmedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();

        var loginResult = await loginHandler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None);
        var refreshResult = await refreshHandler.Handle(
            new RefreshTokenCommand(loginResult.RefreshToken),
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(refreshResult.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.RefreshToken));
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);
        Assert.NotEqual(loginResult.AccessToken, refreshResult.AccessToken);
        Assert.Equal(loginResult.User.Id, refreshResult.User.Id);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenRefreshTokenIsReused()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Handle_ShouldThrowUnauthorized_WhenRefreshTokenIsReused));
        await TestServiceFactory.SeedConfirmedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();

        var loginResult = await loginHandler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None);
        var command = new RefreshTokenCommand(loginResult.RefreshToken);

        await refreshHandler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            refreshHandler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenRefreshTokenIsUnknown()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Handle_ShouldThrowUnauthorized_WhenRefreshTokenIsUnknown));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RefreshTokenCommand("unknown-token"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRule_WhenTenantIsMissing()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Handle_ShouldThrowBusinessRule_WhenTenantIsMissing));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new RefreshTokenCommand("any-token"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_ShouldPersistHashedRefreshToken_NotRawValue()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Login_ShouldPersistHashedRefreshToken_NotRawValue));
        await TestServiceFactory.SeedConfirmedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var loginResult = await loginHandler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None);
        var stored = Assert.Single(db.Set<RefreshToken>().ToList());

        Assert.NotEqual(loginResult.RefreshToken, stored.Token);
        Assert.Equal(RefreshToken.HashToken(loginResult.RefreshToken), stored.Token);
    }

    #endregion
}
