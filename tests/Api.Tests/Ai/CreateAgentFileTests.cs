using System.Net;
using System.Net.Http.Json;
using Api.Features.Ai;
using Api.Features.Tenants;
using Api.Shared;
using Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class CreateAgentFileTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;
    private const string TestPassword = "TestPassword1!";

    [Fact]
    public async Task Handle_ShouldCreateFile_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldCreateFile_WhenInputIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var agent = await CreateExtraAgentAsync(scope.ServiceProvider);
        var handler = scope.ServiceProvider.GetRequiredService<CreateAgentFileHandler>();

        var result = await handler.Handle(
            new CreateAgentFileCommand(agent.AgentId, "notes.txt", "hello file"),
            CancellationToken.None);

        Assert.Equal("notes.txt", result.Name);
        Assert.NotEqual(Guid.Empty, result.FileId);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenAgentIsInAnotherTenant()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenAgentIsInAnotherTenant));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var agent = await CreateExtraAgentAsync(scope.ServiceProvider);

        TestServiceFactory.SetTenant(scope.ServiceProvider, WellKnownTenants.Public);
        var handler = scope.ServiceProvider.GetRequiredService<CreateAgentFileHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new CreateAgentFileCommand(agent.AgentId, "x.txt", "nope"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "true";
            settings["Seed:AdminPassword"] = TestPassword;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await client.GetAsync($"/api/v1/ai/agents/{Guid.NewGuid()}/files");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<AgentOutput> CreateExtraAgentAsync(IServiceProvider sp) =>
        await sp.GetRequiredService<CreateAgentHandler>().Handle(
            new CreateAgentCommand("With files", "x", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);
}

public sealed class ListAgentFilesTests
{
    [Fact]
    public async Task Handle_ShouldListOnlyThatAgentsFiles()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldListOnlyThatAgentsFiles));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var createAgent = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var createFile = scope.ServiceProvider.GetRequiredService<CreateAgentFileHandler>();
        var list = scope.ServiceProvider.GetRequiredService<ListAgentFilesHandler>();

        var first = await createAgent.Handle(
            new CreateAgentCommand("A", "a", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);
        var second = await createAgent.Handle(
            new CreateAgentCommand("B", "b", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        await createFile.Handle(
            new CreateAgentFileCommand(first.AgentId, "a.txt", "aaa"),
            CancellationToken.None);
        await createFile.Handle(
            new CreateAgentFileCommand(second.AgentId, "b.txt", "bbb"),
            CancellationToken.None);

        var listed = await list.Handle(new ListAgentFilesQuery(first.AgentId), CancellationToken.None);

        Assert.Single(listed);
        Assert.Equal("a.txt", listed[0].Name);
    }
}

public sealed class DeleteAgentFileTests
{
    [Fact]
    public async Task Handle_ShouldRemoveFile_WhenFileExists()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldRemoveFile_WhenFileExists));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var createAgent = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var createFile = scope.ServiceProvider.GetRequiredService<CreateAgentFileHandler>();
        var delete = scope.ServiceProvider.GetRequiredService<DeleteAgentFileHandler>();
        var get = scope.ServiceProvider.GetRequiredService<GetAgentFileHandler>();

        var agent = await createAgent.Handle(
            new CreateAgentCommand("Docs", "x", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);
        var file = await createFile.Handle(
            new CreateAgentFileCommand(agent.AgentId, "gone.txt", "bye"),
            CancellationToken.None);

        await delete.Handle(new DeleteAgentFileCommand(agent.AgentId, file.FileId), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            get.Handle(new GetAgentFileQuery(agent.AgentId, file.FileId), CancellationToken.None));
    }
}

public sealed class AgentFileToolTests
{
    [Fact]
    public async Task ReadAgentFile_ShouldReturnContent_ForSameAgent()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(ReadAgentFile_ShouldReturnContent_ForSameAgent));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var createAgent = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var createFile = scope.ServiceProvider.GetRequiredService<CreateAgentFileHandler>();
        var runtime = scope.ServiceProvider.GetRequiredService<IAgentRuntimeContext>();
        var tool = scope.ServiceProvider.GetRequiredService<IEnumerable<ITool>>()
            .Single(t => t.Definition.Name == AgentToolNames.ReadAgentFile);

        var agent = await createAgent.Handle(
            new CreateAgentCommand(
                "Reader",
                "x",
                [AgentToolNames.ListAgentFiles, AgentToolNames.ReadAgentFile]),
            CancellationToken.None);
        var file = await createFile.Handle(
            new CreateAgentFileCommand(agent.AgentId, "kb.txt", "secret-body"),
            CancellationToken.None);

        runtime.Set(agent.AgentId);
        var json = await tool.ExecuteAsync(
            new ToolCall("c1", AgentToolNames.ReadAgentFile, new System.Text.Json.Nodes.JsonObject
            {
                ["file_id"] = file.FileId.ToString()
            }),
            CancellationToken.None);

        Assert.Contains("secret-body", json);
    }

    [Fact]
    public void Module_ShouldNotExposeVectorOrMcpEndpoints()
    {
        var root = RepoRoot();
        var features = File.ReadAllText(Path.Combine(root, "features.json"));
        Assert.DoesNotContain("vector", features, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mcp", features, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("embedding", features, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "features.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
