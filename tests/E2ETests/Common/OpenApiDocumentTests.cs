using System.Net;
using System.Text.Json;
using E2ETests.Common;

namespace E2ETests.Common;

/// <summary>
/// The committed `src/Api/openapi.json` is the contract other tools read: the architecture tests,
/// the front-end route guard, and any client generator. This regenerates it from the running
/// endpoints and fails when the two drift, so a contract change shows up in the diff of its PR.
/// </summary>
public sealed class OpenApiDocumentTests
{
    private const string DocumentPath = "/openapi/v1.json";

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

    public static string DocumentFile => Path.Combine(RepoRoot, "src", "Api", "openapi.json");

    [Fact]
    public async Task Document_ShouldMatch_TheCommittedContract()
    {
        await using var factory = E2EWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(DocumentPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var generated = Normalize(await response.Content.ReadAsStringAsync());

        // Set UPDATE_OPENAPI=1 to accept the new contract after changing an endpoint.
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            await File.WriteAllTextAsync(DocumentFile, generated + Environment.NewLine);
            return;
        }

        Assert.True(
            File.Exists(DocumentFile),
            $"Missing {DocumentFile}. Run: UPDATE_OPENAPI=1 dotnet test tests/E2ETests");

        var committed = Normalize(await File.ReadAllTextAsync(DocumentFile));

        Assert.True(
            committed == generated,
            "src/Api/openapi.json is out of date. Run: UPDATE_OPENAPI=1 dotnet test tests/E2ETests");
    }

    /// <summary>Drops `servers`, which carries the host the document happened to be served from.</summary>
    private static string Normalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var stable = document.RootElement
            .EnumerateObject()
            .Where(property => property.Name != "servers")
            .ToDictionary(property => property.Name, property => property.Value);

        return JsonSerializer.Serialize(stable, new JsonSerializerOptions { WriteIndented = true });
    }
}
