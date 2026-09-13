using Api.Features.Identity;
using Api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Identity;

public sealed class LogoutTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldNoOp_WhenTokenIsMissing()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Handle_ShouldNoOp_WhenTokenIsMissing));
        await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<LogoutHandler>();

        var revoked = await handler.Handle(new LogoutCommand(null), CancellationToken.None);

        Assert.False(revoked);
    }

    [Fact]
    public async Task Handle_ShouldRevoke_WhenTokenIsActive()
    {
        var provider = TestServiceFactory.CreateWithAuth(nameof(Handle_ShouldRevoke_WhenTokenIsActive));
        await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        var login = await loginHandler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None);

        var handler = scope.ServiceProvider.GetRequiredService<LogoutHandler>();
        var revoked = await handler.Handle(new LogoutCommand(login.RefreshToken), CancellationToken.None);

        Assert.True(revoked);

        var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            refreshHandler.Handle(new RefreshTokenCommand(login.RefreshToken), CancellationToken.None));
    }
}

public sealed class RefreshCookieTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Options_ShouldSetSecure_ByScheme(bool isHttps)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = isHttps ? "https" : "http";

        var options = RefreshCookie.OptionsFor(context, expirationDays: 30);

        Assert.Equal(isHttps, options.Secure);
        Assert.True(options.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
        Assert.Equal("/api/v1/identity", options.Path);
        Assert.Equal(TimeSpan.FromDays(30), options.MaxAge);
    }
}
