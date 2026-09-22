using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public sealed record ModelOutput(
    string Id,
    string Name,
    int? ContextLength,
    decimal? InputPricePerToken,
    decimal? OutputPricePerToken);

public interface IModelCatalog
{
    /// <summary>Models the active provider accepts with tools. Throws <see cref="ServiceUnavailableException"/> when the provider catalog cannot be read.</summary>
    Task<IReadOnlyList<ModelOutput>> ListAsync(CancellationToken cancellationToken = default);
}

public static class ModelCatalogExtensions
{
    public static async Task<bool> ContainsAsync(
        this IModelCatalog catalog,
        string modelId,
        CancellationToken cancellationToken = default)
    {
        var models = await catalog.ListAsync(cancellationToken);
        return models.Any(model => string.Equals(model.Id, modelId, StringComparison.Ordinal));
    }
}

internal sealed class OpenRouterModelCatalog(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptions<LlmOptions> options,
    ILogger<OpenRouterModelCatalog> logger) : IModelCatalog
{
    private const string CacheKey = "ai:model-catalog:openrouter";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<ModelOutput>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<ModelOutput>? cached) && cached is not null)
            return cached;

        var models = await FetchAsync(cancellationToken);
        cache.Set(CacheKey, models, CacheDuration);
        return models;
    }

    private async Task<IReadOnlyList<ModelOutput>> FetchAsync(CancellationToken cancellationToken)
    {
        OpenRouterModelsResponse? body;
        try
        {
            var client = httpClientFactory.CreateClient(OpenRouterLlmService.HttpClientName);
            using var response = await client.GetAsync("models", cancellationToken);
            response.EnsureSuccessStatusCode();
            body = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "OpenRouter model catalog request failed");
            throw new ServiceUnavailableException("Catálogo de modelos indisponível.", ex);
        }

        var allowed = options.Value.AllowedModels.ToHashSet(StringComparer.Ordinal);
        return (body?.Data ?? [])
            .Where(model => model.SupportedParameters?.Contains("tools", StringComparer.Ordinal) == true)
            .Where(model => allowed.Count == 0 || allowed.Contains(model.Id))
            .Select(model => new ModelOutput(
                model.Id,
                string.IsNullOrWhiteSpace(model.Name) ? model.Id : model.Name,
                model.ContextLength,
                ParsePrice(model.Pricing?.Prompt),
                ParsePrice(model.Pricing?.Completion)))
            .OrderBy(model => model.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static decimal? ParsePrice(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var price) ? price : null;

    private sealed record OpenRouterModelsResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<OpenRouterModel>? Data);

    private sealed record OpenRouterModel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("context_length")] int? ContextLength,
        [property: JsonPropertyName("pricing")] OpenRouterPricing? Pricing,
        [property: JsonPropertyName("supported_parameters")] IReadOnlyList<string>? SupportedParameters);

    private sealed record OpenRouterPricing(
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("completion")] string? Completion);
}

/// <summary>Microsoft Agent Framework binds its client to one model at construction, so only configured models are offered.</summary>
internal sealed class ConfiguredModelCatalog(IOptions<LlmOptions> options) : IModelCatalog
{
    public Task<IReadOnlyList<ModelOutput>> ListAsync(CancellationToken cancellationToken = default)
    {
        var llm = options.Value;
        IReadOnlyList<ModelOutput> models = new[] { llm.Model }
            .Concat(llm.AllowedModels)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new ModelOutput(id, id, null, null, null))
            .ToList();
        return Task.FromResult(models);
    }
}

internal sealed class StubModelCatalog(IOptions<LlmOptions> options) : IModelCatalog
{
    public const string ModelA = "stub/model-a";
    public const string ModelB = "stub/model-b";

    public Task<IReadOnlyList<ModelOutput>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModelOutput> models = new[] { options.Value.Model, ModelA, ModelB }
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new ModelOutput(id, id, null, 0m, 0m))
            .ToList();
        return Task.FromResult(models);
    }
}
