using Api.Features.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Ai;

public sealed class AgentLoopUsageTests
{
    [Fact]
    public async Task RunAsync_ShouldSumTokensAndCost()
    {
        var calls = 0;
        var llm = new ScriptedLlmService((_, _) =>
        {
            calls++;
            return Task.FromResult(calls == 1
                ? new LlmResponse(string.Empty, 15, [new ToolCall("c1", "unknown_tool", [])], InputTokens: 10, OutputTokens: 5, Cost: 0.001m)
                : new LlmResponse("done", 27, InputTokens: 20, OutputTokens: 7, Cost: 0.0025m));
        });
        var loop = new AgentLoop(llm, new ToolRegistry([]), NullLogger<AgentLoop>.Instance);

        var result = await loop.RunAsync("go", "system", null, ["unknown_tool"]);

        Assert.Equal(30, result.InputTokens);
        Assert.Equal(12, result.OutputTokens);
        Assert.Equal(0.0035m, result.Cost);
        Assert.Equal(2, result.IterationsUsed);
    }

    [Fact]
    public async Task RunAsync_ShouldReturnNullCost_WhenAnyCallCostIsNull()
    {
        var calls = 0;
        var llm = new ScriptedLlmService((_, _) =>
        {
            calls++;
            return Task.FromResult(calls == 1
                ? new LlmResponse(string.Empty, 2, [new ToolCall("c1", "unknown_tool", [])], InputTokens: 1, OutputTokens: 1, Cost: 0.001m)
                : new LlmResponse("done", 2, InputTokens: 1, OutputTokens: 1, Cost: null));
        });
        var loop = new AgentLoop(llm, new ToolRegistry([]), NullLogger<AgentLoop>.Instance);

        var result = await loop.RunAsync("go", "system", null, ["unknown_tool"]);

        Assert.Null(result.Cost);
        Assert.Equal(2, result.InputTokens);
    }
}
