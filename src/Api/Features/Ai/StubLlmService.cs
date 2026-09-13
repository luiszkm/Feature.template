using System.Text.Json.Nodes;

namespace Api.Features.Ai;

/// <summary>
/// Development/test LLM that returns direct answers or simulated tool calls.
/// Replace with Azure OpenAI / OpenAI when Ai:LlmProvider is configured.
/// </summary>
internal sealed class StubLlmService : ILlmService
{
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var prompt = request.UserPrompt.Trim().ToLowerInvariant();

        if (request.Tools is { Count: > 0 } && string.IsNullOrEmpty(request.UserPrompt))
        {
            return Task.FromResult(new LlmResponse(
                "Com base nos dados consultados, aqui está o resumo solicitado.",
                TotalTokens: 42));
        }

        if (request.Tools is { Count: > 0 } && prompt.Contains("user"))
        {
            return Task.FromResult(new LlmResponse(
                string.Empty,
                TotalTokens: 10,
                ToolCalls:
                [
                    new ToolCall(
                        "call_users",
                        "get_users_summary",
                        new JsonObject { ["page_size"] = 10 })
                ]));
        }

        if (request.Tools is { Count: > 0 } && prompt.Contains("tenant"))
        {
            return Task.FromResult(new LlmResponse(
                string.Empty,
                TotalTokens: 10,
                ToolCalls:
                [
                    new ToolCall(
                        "call_tenants",
                        "get_tenant_info",
                        new JsonObject { ["page_size"] = 20 })
                ]));
        }

        var reply = string.IsNullOrWhiteSpace(request.UserPrompt)
            ? "Não há mais dados para consultar."
            : $"Recebi sua mensagem: {request.UserPrompt}";

        return Task.FromResult(new LlmResponse(reply, TotalTokens: 20));
    }
}
