using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchitectureTests;

/// <summary>
/// `features.json` and the OpenAPI document are two views of the same surface. A route that exists
/// in one and not in the other is drift, and it is invisible until a screen breaks at runtime.
/// </summary>
public sealed class OpenApiContractTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "features.json"))
                    && Directory.Exists(Path.Combine(dir.FullName, "src", "Api")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root from test base directory.");
        }
    }

    private static string DocumentPath => Path.Combine(RepoRoot, "src", "Api", "openapi.json");

    private static IReadOnlySet<string> FeatureRoutes()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot, "features.json"));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
            .EnumerateObject()
            .Select(slice => slice.Value.GetProperty("route").GetString()!)
            .Select(Canonical)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> DocumentedRoutes()
    {
        Assert.True(
            File.Exists(DocumentPath),
            $"Missing {DocumentPath}. Run: UPDATE_OPENAPI=1 dotnet test tests/E2ETests");

        using var stream = File.OpenRead(DocumentPath);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .Where(route => route.Name.StartsWith("/api/v1/", StringComparison.Ordinal))
            .SelectMany(route => route.Value
                .EnumerateObject()
                .Select(operation => Canonical($"{operation.Name} {route.Name}")))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>`POST /api/v1/tenants/{tenantId}` - upper-cased verb, route template as written.</summary>
    private static string Canonical(string route)
    {
        var parts = route.Split(' ', 2, StringSplitOptions.TrimEntries);
        return $"{parts[0].ToUpperInvariant()} {parts[1]}";
    }

    [Fact]
    public void EveryFeatureRoute_ShouldExist_InTheDocument()
    {
        var missing = FeatureRoutes().Except(DocumentedRoutes()).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            "Routes in features.json missing from openapi.json: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryDocumentedRoute_ShouldExist_InFeaturesJson()
    {
        var missing = DocumentedRoutes().Except(FeatureRoutes()).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            "Routes in openapi.json missing from features.json: " + string.Join(", ", missing));
    }

    [Fact]
    public void Document_ShouldCover_EveryFeatureRoute()
    {
        var routes = DocumentedRoutes();

        Assert.Equal(FeatureRoutes().Count, routes.Count);
        Assert.Contains("POST /api/v1/identity/login", routes);
    }

    /// <summary>
    /// The rate limiter is shared configuration, so a route joining the `auth` policy inherits a
    /// `429` that nobody remembers to declare. The route-level guards above compare paths and
    /// methods, so a missing status is exactly what they cannot see.
    /// </summary>
    [Fact]
    public void EveryRateLimitedRoute_ShouldDeclare_TooManyRequests()
    {
        var featuresRoot = Path.Combine(RepoRoot, "src", "Api", "Features");

        var rateLimited = Directory
            .EnumerateFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Where(text => text.Contains("AuthRateLimitPolicy", StringComparison.Ordinal))
            .SelectMany(text => Regex
                .Matches(text, @"Map(Get|Post|Put|Delete)\(""(/api/v1/[^""]+)""")
                .Select(match => new
                {
                    Method = match.Groups[1].Value.ToLowerInvariant(),
                    Route = Regex.Replace(match.Groups[2].Value, @":[a-z]+\}", "}")
                }))
            .ToList();

        Assert.NotEmpty(rateLimited);

        using var stream = File.OpenRead(DocumentPath);
        using var document = JsonDocument.Parse(stream);
        var paths = document.RootElement.GetProperty("paths");

        var undeclared = rateLimited
            .Where(endpoint =>
                !paths.TryGetProperty(endpoint.Route, out var route)
                || !route.TryGetProperty(endpoint.Method, out var operation)
                || !operation.GetProperty("responses").TryGetProperty("429", out _))
            .Select(endpoint => $"{endpoint.Method.ToUpperInvariant()} {endpoint.Route}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            undeclared.Count == 0,
            "Rate-limited routes not declaring 429 in openapi.json: " + string.Join(", ", undeclared));
    }
}
