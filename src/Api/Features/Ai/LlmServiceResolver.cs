using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

internal static class LlmServiceResolver
{
    public static bool UseStub(IHostEnvironment environment, LlmOptions options)
    {
        if (environment.IsEnvironment("Testing"))
            return true;

        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.ApiKey))
            return true;

        return false;
    }

    public const string StubProvider = "stub";

    public static string ProviderLabel(IHostEnvironment environment, LlmOptions options) =>
        UseStub(environment, options) ? StubProvider : LlmProviders.Normalize(options.Provider);

    public static bool IsMicrosoftAgentFramework(IHostEnvironment environment, LlmOptions options) =>
        !UseStub(environment, options)
        && string.Equals(
            LlmProviders.Normalize(options.Provider),
            LlmProviders.MicrosoftAgentFramework,
            StringComparison.Ordinal);

    public static string EffectiveBaseUrl(LlmOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            return options.BaseUrl.TrimEnd('/');

        return string.Equals(
            LlmProviders.Normalize(options.Provider),
            LlmProviders.MicrosoftAgentFramework,
            StringComparison.Ordinal)
            ? "https://api.openai.com/v1"
            : OpenRouterLlmService.DefaultBaseUrl;
    }
}

internal sealed class LlmOptionsValidator(
    IHostEnvironment environment,
    IConfiguration configuration)
{
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        var enableAi = configuration.GetValue("FeatureFlags:EnableAI", false);
        if (!enableAi || environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;

        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.ApiKey))
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                "Ai:Llm:ApiKey must be configured when FeatureFlags:EnableAI is true. " +
                "Set Ai:Llm:ApiKey via environment (AI_LLM_API_KEY), user-secrets, or compose.env.");
        }

        if (!LlmProviders.IsKnown(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"Ai:Llm:Provider '{options.Provider}' is not supported. " +
                $"Use '{LlmProviders.MicrosoftAgentFramework}' or '{LlmProviders.OpenRouter}'.");
        }

        return ValidateOptionsResult.Success;
    }
}

internal sealed class LlmStartupGuard(
    IHostEnvironment environment,
    IConfiguration configuration,
    IOptions<LlmOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var result = new LlmOptionsValidator(environment, configuration).Validate(null, options.Value);
        if (result.Failed)
            throw new InvalidOperationException(result.FailureMessage);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
