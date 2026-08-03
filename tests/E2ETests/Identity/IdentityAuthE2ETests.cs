using System.Net;
using System.Net.Http.Json;
using App.Features.Identity;
using E2ETests.Common;

namespace E2ETests.Identity;

public sealed class IdentityAuthE2ETests
{
    private const string TestPassword = "TestPassword1!";

    [Fact]
    public async Task RegisterAndLogin_ShouldSucceed_WhenEmailIsConfirmed()
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
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterUserResponse>();
        Assert.NotNull(registered);
        Assert.False(string.IsNullOrWhiteSpace(registered.EmailConfirmationToken));

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/v1/identity/users/{registered.Id}/confirm-email",
            new { token = registered.EmailConfirmationToken });
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenOutput>();
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
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenOutput>();
        Assert.NotNull(auth);
        Assert.Contains("Admin", auth.User.Roles);
    }
}
