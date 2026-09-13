using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchitectureTests;

/// <summary>
/// Without a solution listing every project, `dotnet build` at the root fails with MSB1003 and the
/// CI build step goes with it. The GUIDs are the ones `dotnet new` regenerates per generated
/// product, so they have to stay in step with the template config.
/// </summary>
public sealed class SolutionFileTests
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

    private static string SolutionText =>
        File.ReadAllText(Path.Combine(RepoRoot, "Product.Template.sln"));

    [Fact]
    public void Solution_ShouldListEveryProject()
    {
        var projects = Directory
            .EnumerateFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepoRoot, path).Replace(Path.DirectorySeparatorChar, '\\'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(projects);

        var solution = SolutionText;
        var missing = projects.Where(project => !solution.Contains(project, StringComparison.Ordinal)).ToList();

        Assert.True(
            missing.Count == 0,
            "Projects missing from Product.Template.sln: " + string.Join(", ", missing));
    }

    [Fact]
    public void ProjectGuids_ShouldMatch_TemplateConfig()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot, ".template.config", "template.json"));
        using var document = JsonDocument.Parse(stream);

        var templateGuids = document.RootElement
            .GetProperty("guids")
            .EnumerateArray()
            .Select(element => element.GetString()!.ToUpperInvariant())
            .OrderBy(guid => guid, StringComparer.Ordinal)
            .ToList();

        var solutionGuids = Regex
            .Matches(
                SolutionText,
                """^Project\("\{[^}]+\}"\) = "[^"]+", "[^"]+", "\{([0-9A-Fa-f-]+)\}"$""",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value.ToUpperInvariant())
            .OrderBy(guid => guid, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(templateGuids, solutionGuids);
    }
}
