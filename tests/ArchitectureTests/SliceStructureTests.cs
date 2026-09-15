using System.Reflection;
using System.Text.Json;
using NetArchTest.Rules;

namespace ArchitectureTests;

public sealed class SliceStructureTests
{
    private static readonly Assembly AppAssembly = typeof(Api.Features.Identity.RegisterUserHandler).Assembly;

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

    [Fact]
    public void Shared_ShouldNotReference_Features()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .ResideInNamespaceStartingWith("Api.Shared")
            .And()
            .DoNotResideInNamespaceStartingWith("Api.Shared.Migrations")
            .ShouldNot()
            .HaveDependencyOn("Api.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Features_ShouldNotContain_LayerFolders()
    {
        var featuresRoot = Path.Combine(RepoRoot, "src", "Api", "Features");
        Assert.True(Directory.Exists(featuresRoot), $"Missing Features root: {featuresRoot}");

        string[] forbidden =
        [
            "Handlers", "Validators", "Mappers", "Controllers", "Services",
            "Repositories", "DTOs", "Interfaces", "Models"
        ];

        var violations = Directory.EnumerateDirectories(featuresRoot, "*", SearchOption.AllDirectories)
            .Where(path => forbidden.Contains(new DirectoryInfo(path).Name, StringComparer.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(RepoRoot, path))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(violations.Count == 0, "Layer folders under Features are forbidden: " + string.Join(", ", violations));
    }

    [Fact]
    public void PublicContracts_ShouldNotExpose_GenericIRepository()
    {
        var genericRepos = AppAssembly.GetTypes()
            .Where(t => t.IsPublic && t.IsGenericTypeDefinition && t.Name == "IRepository`1")
            .Select(t => t.FullName)
            .ToList();

        Assert.True(genericRepos.Count == 0,
            "Public generic IRepository<> is forbidden: " + string.Join(", ", genericRepos));
    }

    [Fact]
    public void Program_Cs_ShouldStay_UnderLineBudget()
    {
        var programPath = Path.Combine(RepoRoot, "src", "Api", "Program.cs");
        var lines = File.ReadAllLines(programPath);
        var nonEmpty = lines.Count(l => !string.IsNullOrWhiteSpace(l));

        Assert.True(nonEmpty <= 40, $"Program.cs has {nonEmpty} non-empty lines; budget is 40.");
    }

    [Fact]
    public void All_IEndpoint_Implementations_ShouldBe_Discoverable()
    {
        var endpointTypes = AppAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && typeof(Api.Shared.IEndpoint).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(endpointTypes);

        // Same discovery rule as EndpointRegistration.AddEndpoints — every concrete IEndpoint is registered.
        var discoverable = AppAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(Api.Shared.IEndpoint).IsAssignableFrom(t))
            .ToHashSet();

        Assert.Equal(endpointTypes.Count, discoverable.Count);
        Assert.All(endpointTypes, t => Assert.Contains(t, discoverable));
    }

    [Fact]
    public void FeaturesJson_AppPaths_ShouldExist()
    {
        var featuresPath = Path.Combine(RepoRoot, "features.json");
        using var stream = File.OpenRead(featuresPath);
        using var doc = JsonDocument.Parse(stream);

        var missing = new List<string>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (!property.Value.TryGetProperty("app", out var appProp))
                continue;

            var relative = appProp.GetString();
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var fullPath = Path.Combine(RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                missing.Add($"{property.Name}: {relative}");
        }

        Assert.True(missing.Count == 0, "features.json app paths missing: " + string.Join("; ", missing));
    }

    [Fact]
    public void Features_ShouldNotDependOn_OtherFeatureModules()
    {
        var featuresRoot = Path.Combine(RepoRoot, "src", "Api", "Features");
        Assert.True(Directory.Exists(featuresRoot), $"Missing Features root: {featuresRoot}");

        var modules = Directory.GetDirectories(featuresRoot)
            .Select(path => new DirectoryInfo(path).Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Contains("Identity", modules);
        Assert.Contains("Authorization", modules);

        // Cross-module type coupling must go through Shared contracts.
        // Keep this empty; a new pair is a documented leftover, not a silent exception.
        var allowed = new HashSet<(string From, string To)>();

        var failures = new List<string>();
        foreach (var from in modules)
        {
            foreach (var to in modules)
            {
                if (from == to || allowed.Contains((from, to)))
                    continue;

                var result = Types.InAssembly(AppAssembly)
                    .That()
                    .ResideInNamespaceStartingWith($"Api.Features.{from}")
                    .ShouldNot()
                    .HaveDependencyOn($"Api.Features.{to}")
                    .GetResult();

                if (result.IsSuccessful)
                    continue;

                var types = string.Join(", ", result.FailingTypeNames ?? []);
                failures.Add($"{from} → {to}: {types}");
            }
        }

        Assert.True(failures.Count == 0,
            "Feature modules must not reference each other. Add a Shared contract or a documented allowlist entry. "
            + string.Join("; ", failures));
    }

    [Fact]
    public void Features_ShouldNotDependOn_HostSecurity()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .ResideInNamespaceStartingWith("Api.Features")
            .ShouldNot()
            .HaveDependencyOn("Api.Host.Security")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void GlobalUsings_ShouldNotImport_FeatureModules()
    {
        var path = Path.Combine(RepoRoot, "src", "Api", "GlobalUsings.cs");
        var text = File.ReadAllText(path);

        Assert.DoesNotContain("global using Api.Features", text, StringComparison.Ordinal);
    }
}
