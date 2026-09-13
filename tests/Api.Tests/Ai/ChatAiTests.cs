using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Features.Identity;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Ai;

public sealed class ChatAiTests
{
    private const string TestPassword = "TestPassword1!";

    [Fact]
    public async Task ChatAi_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "false";
            settings["Seed:AdminPassword"] = TestPassword;
        });

        using var client = await CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new { message = "hello" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
            settings["FeatureFlags:EnableAI"] = "true");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new { message = "hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatAi_ShouldReturn200_WhenEnableAiIsTrueAndAuthenticated()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "true";
            settings["Seed:AdminPassword"] = TestPassword;
        });

        using var client = await CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email = "admin@producttemplate.com",
            password = TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenOutput>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
