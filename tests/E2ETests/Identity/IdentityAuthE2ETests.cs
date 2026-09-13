using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Api.Features.Identity;
using E2ETests.Common;

namespace E2ETests.Identity;

public sealed class IdentityAuthE2ETests
{
    private const string TestPassword = "TestPassword1!";

    [Fact]
    public async Task RegisterAndLogin_ShouldSucceed()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var email = $"e2e-{Guid.NewGuid():N}@test.com";
        const string password = "Password1!";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email,
            password,
            firstName = "E2E",
            lastName = "User"
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<UserOutput>();
        Assert.NotNull(registered);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
    }

    [Fact]
    public async Task AdminLogin_ShouldReturnToken_WhenSeedCompleted()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email = "admin@producttemplate.com",
            password = TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(auth);
        Assert.Contains("Admin", auth.User.Roles);
    }

    [Fact]
    public async Task Login_ShouldSetHttpOnlyRefreshCookie()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var response = await LoginAsAdmin(client);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("pt_refresh=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/identity", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldNotReturnRefreshTokenInBody()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var response = await LoginAsAdmin(client);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.False(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Refresh_ShouldRotateTheCookie()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var login = await LoginAsAdmin(client);
        var firstCookie = RefreshCookieValue(login);

        var refresh = await PostWithCookie(client, "/api/v1/identity/refresh", firstCookie);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var secondCookie = RefreshCookieValue(refresh);
        Assert.NotEqual(firstCookie, secondCookie);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ShouldReturn401()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var response = await client.PostAsync("/api/v1/identity/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldRevokeTheRefreshToken()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var login = await LoginAsAdmin(client);
        var cookie = RefreshCookieValue(login);

        var logout = await PostWithCookie(client, "/api/v1/identity/logout", cookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(
            logout.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith("pt_refresh=;", StringComparison.Ordinal));

        var refresh = await PostWithCookie(client, "/api/v1/identity/refresh", cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCookie_ShouldReturn204_AndTouchNothing()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var login = await LoginAsAdmin(client);
        var cookie = RefreshCookieValue(login);

        // No cookie presented: the endpoint has nothing to revoke and says so with 204.
        var logout = await client.PostAsync("/api/v1/identity/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // The untouched token still works, which is what "changed no records" means here.
        var refresh = await PostWithCookie(client, "/api/v1/identity/refresh", cookie);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Login_CookieShouldNotBeSecure_OverPlainHttp()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        var response = await LoginAsAdmin(client);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        // The test host speaks http, so a `Secure` cookie would be dropped by a real browser.
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ShouldReturn429_WhenTheAuthLimiterTrips()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = CreateClient(factory);

        // The `auth` policy permits 20 per minute with no queue; the 21st is rejected.
        HttpStatusCode last = HttpStatusCode.NoContent;
        for (var attempt = 0; attempt < 21 && last != HttpStatusCode.TooManyRequests; attempt++)
        {
            var response = await client.PostAsync("/api/v1/identity/logout", content: null);
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        // Cookies are set by hand so each test states exactly which credential it presents.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");
        return client;
    }

    private static Task<HttpResponseMessage> LoginAsAdmin(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email = "admin@producttemplate.com",
            password = TestPassword
        });

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string route, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Add("Cookie", $"pt_refresh={cookie}");
        return client.SendAsync(request);
    }

    private static string RefreshCookieValue(HttpResponseMessage response)
    {
        var header = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("pt_refresh=", StringComparison.Ordinal));

        return header.Split(';')[0]["pt_refresh=".Length..];
    }
}
