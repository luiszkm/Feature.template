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
        // This listener records but never samples the Ai source: an Ai span exists only when the
        // app's own TracerProvider (EnableTraces + AddSource) asks for it.
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Microsoft.AspNetCore" or AiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                options.Source.Name == AiTelemetry.ActivitySourceName
                    ? ActivitySamplingResult.None
                    : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);

        var on = await RecordAsync(stopped, traces: true);
        var onRequest = Assert.Single(on, a => a.Source.Name == "Microsoft.AspNetCore" && a.GetTagItem("url.path") as string == "/api/v1/ai/chat");
        var invoke = Assert.Single(on, a => a.GetTagItem(AiTelemetry.OperationName) as string == AiTelemetry.InvokeAgent);
        Assert.Equal(onRequest.TraceId, invoke.TraceId);
        Assert.Equal(onRequest.SpanId, invoke.ParentSpanId);
        var chat = Assert.Single(on, a => a.GetTagItem(AiTelemetry.OperationName) as string == AiTelemetry.Chat);
        Assert.Equal(invoke.SpanId, chat.ParentSpanId);
        Assert.Equal("stub", chat.GetTagItem(AiTelemetry.ProviderName));

        var off = await RecordAsync(stopped, traces: false);
        // Without the app's provider nothing enriches the request span, but requests were traced;
        // other E2E classes run in parallel and add their own, none of them from the Ai source.
        Assert.Contains(off, a => a.Source.Name == "Microsoft.AspNetCore");
        Assert.DoesNotContain(off, a => a.Source.Name == AiTelemetry.ActivitySourceName);
    }

    /// <summary>The activities stopped while one chat ran with the given flag.</summary>
    private static async Task<List<Activity>> RecordAsync(ConcurrentQueue<Activity> stopped, bool traces)
    {
        var before = stopped.Count;
        await ChatAsync(traces);
        return stopped.Skip(before).ToList();
    }

    private static async Task ChatAsync(bool traces)
    {
        // AddObservability reads the flag while services are registered; in-memory settings only
        // exist after Build(), so the switch goes in as a host setting, like appsettings or env vars.
        await using var factory = E2EWebApplicationFactory.Create()
            .WithWebHostBuilder(builder => builder.UseSetting("OpenTelemetry:EnableTraces", traces ? "true" : "false"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");
        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new { email = "admin@producttemplate.com", password = "TestPassword1!" });
        var token = (await login.Content.ReadFromJsonAsync<AuthTokenOutput>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new { message = "hello" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
