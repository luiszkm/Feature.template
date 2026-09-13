using System.Text.Json;

namespace ArchitectureTests;

public sealed class TemplateConfigTests
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

    [Theory]
    [InlineData("**/node_modules/**")]
    [InlineData("**/dist/**")]
    public void TemplateJson_ShouldExclude_FrontEndBuildArtifacts(string glob)
    {
        var path = Path.Combine(RepoRoot, ".template.config", "template.json");
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        var excludes = document.RootElement
            .GetProperty("sources")[0]
            .GetProperty("modifiers")[0]
            .GetProperty("exclude")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToList();

        Assert.Contains(glob, excludes);
    }
}
