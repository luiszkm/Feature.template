using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

internal static class ConversationHttp
{
    public static WebApplicationFactory<Program> Factory(bool enableAi = true) =>
        AiHttp.Factory(settings => settings["FeatureFlags:EnableAI"] = enableAi ? "true" : "false")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<ILlmService>(ScriptedLlmService.Replying("reply"))));

    public static async Task<Guid> StartConversationAsync(HttpClient client, string message = "hello")
    {
        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChatAiResponse>())!.ConversationId;
    }

    public static HttpClient Anonymous(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");
        return client;
    }
}

internal static class ConversationHandlers
{
    public static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    public static IServiceProvider Provider(string name) =>
        TestServiceFactory.CreateWithAi(name, services => services.AddSingleton<ILlmService>(ScriptedLlmService.Replying()));

    public static async Task<T> RunAsync<T>(IServiceProvider provider, Guid userId, Func<IServiceProvider, Task<T>> run, params string[] roles)
    {
        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        TestServiceFactory.SetUser(scope.ServiceProvider, userId, roles);
        return await run(scope.ServiceProvider);
    }
}
