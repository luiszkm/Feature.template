using Api.Features.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Ai;

public sealed class AgentLoopTurnTests
{
    private static readonly LlmMessage[] Prior = [new("user", "u0"), new("assistant", "a0")];

    [Fact]
    public async Task RunAsync_ShouldKeepUserMessage_BeforeToolCallTurn_OnLaterCalls()
    {
        var llm = CallsToolOnce();

        await Loop(llm).RunAsync("q", "sys", Prior, ["t"]);

        var second = llm.Requests.ElementAt(1);
        Assert.Equal(new[] { "user", "assistant", "user", "assistant", "tool" }, second.History!.Select(m => m.Role).ToArray());
        Assert.Equal(new[] { "u0", "a0", "q", "" }, second.History!.Take(4).Select(m => m.Content).ToArray());
        Assert.StartsWith("<tool_output>", second.History![4].Content);
        Assert.Equal(string.Empty, second.UserPrompt);
    }

    [Fact]
    public async Task RunAsync_ShouldSendMessage_AsUserPrompt_OnFirstCall()
    {
        var llm = CallsToolOnce();

        await Loop(llm).RunAsync("q", "sys", Prior, ["t"]);

        var first = llm.Requests.First();
        Assert.Equal("q", first.UserPrompt);
        Assert.Equal(new[] { ("user", "u0"), ("assistant", "a0") }, first.History!.Select(m => (m.Role, m.Content)).ToArray());
    }

    [Fact]
    public async Task RunAsync_ShouldKeepUserMessageOnce_InSummaryCall()
    {
        // Always asks for the tool: 5 iterations, then the summary call.
        var llm = new ScriptedLlmService((request, _) => Task.FromResult(
            request.Tools is { Count: > 0 }
                ? new LlmResponse(string.Empty, 1, [new ToolCall("c1", "t", [])])
                : new LlmResponse("summary", 1)));

        await Loop(llm).RunAsync("q", "sys", null, ["t"]);

        var summary = llm.Requests.Last();
        Assert.Null(summary.Tools);
        Assert.Single(summary.History!, m => m.Role == "user" && m.Content == "q");
        Assert.Single(summary.History!, m => m.Role == "user");
    }

    [Fact]
    public async Task RunAsync_ShouldExcludeUserMessage_FromTurnMessages()
    {
        var result = await Loop(CallsToolOnce()).RunAsync("q", "sys", Prior, ["t"]);

        Assert.Equal(new[] { "assistant", "tool" }, result.TurnMessages!.Select(m => m.Role).ToArray());
    }

    private static AgentLoop Loop(ILlmService llm) =>
        new(
            llm,
            new ToolRegistry([new LambdaTool("t", (_, _) => Task.FromResult("abc"))]),
            new AllowAllContentGuard(),
            Options.Create(new GuardrailOptions()),
            NullLogger<AgentLoop>.Instance);

    /// <summary>The first call asks for tool <c>t</c>; once a tool result is in the history, it answers.</summary>
    private static ScriptedLlmService CallsToolOnce() =>
        new((request, _) => Task.FromResult(
            request.History?.Any(m => m.Role == "tool") == true
                ? new LlmResponse("done", 1)
                : new LlmResponse(string.Empty, 1, [new ToolCall("c1", "t", [])])));
}
