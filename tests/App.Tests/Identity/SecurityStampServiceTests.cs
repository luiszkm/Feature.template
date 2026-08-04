using App.Features.Identity;
using App.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Identity;

file static class SecurityStampTestDb
{
    public static string Name(string testMethod) => $"SecurityStamp_{testMethod}";
}

public sealed class SecurityStampServiceTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task ValidateAsync_ShouldReturnTrue_WhenStampMatches()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            SecurityStampTestDb.Name(nameof(ValidateAsync_ShouldReturnTrue_WhenStampMatches)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

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
            SecurityStampTestDb.Name(nameof(ValidateAsync_ShouldReturnFalse_WhenStampDoesNotMatch)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

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
            SecurityStampTestDb.Name(nameof(RegenerateAsync_ShouldInvalidatePreviousStamp)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);
        var oldStamp = user.SecurityStamp;

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var stampService = scope.ServiceProvider.GetRequiredService<ISecurityStampService>();

        await stampService.RegenerateAsync(TenantId, user.Id, CancellationToken.None);

        var oldValid = await stampService.ValidateAsync(TenantId, user.Id, oldStamp, CancellationToken.None);
        Assert.False(oldValid);
    }
}
