using App.Features.Identity;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Identity;

file static class OAuthTestDb
{
    public static string Name(string testMethod) => $"OAuth_{testMethod}";
}

public sealed class SecurityStampServiceTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task ValidateAsync_ShouldReturnTrue_WhenStampMatches()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            OAuthTestDb.Name(nameof(ValidateAsync_ShouldReturnTrue_WhenStampMatches)));
        var user = await TestServiceFactory.SeedConfirmedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var stampService = scope.ServiceProvider.GetRequiredService<ISecurityStampService>();

        var isValid = await stampService.ValidateAsync(TenantId, user.Id, user.SecurityStamp, CancellationToken.None);

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnFalse_WhenStampDoesNotMatch()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            OAuthTestDb.Name(nameof(ValidateAsync_ShouldReturnFalse_WhenStampDoesNotMatch)));
        var user = await TestServiceFactory.SeedConfirmedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var stampService = scope.ServiceProvider.GetRequiredService<ISecurityStampService>();

        var isValid = await stampService.ValidateAsync(TenantId, user.Id, "invalid-stamp", CancellationToken.None);

        Assert.False(isValid);
    }

    [Fact]
    public async Task RegenerateAsync_ShouldInvalidatePreviousStamp()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            OAuthTestDb.Name(nameof(RegenerateAsync_ShouldInvalidatePreviousStamp)));
        var user = await TestServiceFactory.SeedConfirmedUserAsync(provider, TenantId);
        var oldStamp = user.SecurityStamp;

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var stampService = scope.ServiceProvider.GetRequiredService<ISecurityStampService>();

        await stampService.RegenerateAsync(TenantId, user.Id, CancellationToken.None);

        var oldValid = await stampService.ValidateAsync(TenantId, user.Id, oldStamp, CancellationToken.None);
        Assert.False(oldValid);
    }
}

public sealed class ExternalLoginTests
{
    [Fact]
    public void Validator_ShouldFail_WhenProviderIsEmpty()
    {
        var validator = new ExternalLoginValidator();
        var result = validator.TestValidate(new ExternalLoginCommand("", "code"));
        result.ShouldHaveValidationErrorFor(x => x.Provider);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenProviderIsNotAvailable()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            OAuthTestDb.Name(nameof(Handle_ShouldThrow_WhenProviderIsNotAvailable)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ExternalLoginHandler>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ExternalLoginCommand("google", "code"), CancellationToken.None));
    }
}

public sealed class GetAuthProvidersTests
{
    [Fact]
    public void Factory_ShouldReturnEmptyProviders_WhenMicrosoftAuthDisabled()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            OAuthTestDb.Name(nameof(Factory_ShouldReturnEmptyProviders_WhenMicrosoftAuthDisabled)));

        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<App.Host.Security.IAuthenticationProviderFactory>();

        Assert.Empty(factory.GetAvailableProviders());
    }
}
