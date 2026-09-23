using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Features.Identity;
using E2ETests.Common;
using Microsoft.AspNetCore.Hosting;

namespace E2ETests.Ai;

public sealed class AiTelemetryE2ETests
{
    [Fact]
    public async Task InvokeAgentSpan_ShouldShareTraceId_WithAspNetCoreRequestSpan()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Microsoft.AspNetCore" or AiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);

        // AddObservability reads the flag while services are registered; in-memory settings only
        // exist after Build(), so the switch goes in as a host setting, like appsettings or env vars.
        await using var factory = E2EWebApplicationFactory.Create()
            .WithWebHostBuilder(builder => builder.UseSetting("OpenTelemetry:EnableTraces", "true"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");
        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new { email = "admin@producttemplate.com", password = "TestPassword1!" });
        var token = (await login.Content.ReadFromJsonAsync<AuthTokenOutput>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoke = Assert.Single(stopped, a => a.Source.Name == AiTelemetry.ActivitySourceName
            && a.GetTagItem(AiTelemetry.OperationName) as string == AiTelemetry.InvokeAgent);
        var request = Assert.Single(stopped, a => a.Source.Name == "Microsoft.AspNetCore" && a.SpanId == invoke.ParentSpanId);
        Assert.Equal(request.TraceId, invoke.TraceId);
        Assert.Equal("/api/v1/ai/chat", request.GetTagItem("url.path"));
    }
}
