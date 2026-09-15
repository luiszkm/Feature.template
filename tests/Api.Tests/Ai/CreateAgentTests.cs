using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Ai;
using Api.Features.Identity;
using Api.Features.Tenants;
using Api.Shared;
using Api.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Ai;

public sealed class CreateAgentTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;
    private const string TestPassword = "TestPassword1!";

    [Fact]
    public void Validator_ShouldFail_WhenNameIsEmpty()
    {
        var tools = new ToolRegistry([]);
        var validator = new CreateAgentValidator(tools);
        var result = validator.TestValidate(new CreateAgentCommand("", "instr", []));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validator_ShouldFail_WhenToolNameIsUnknown()
    {
        var tools = new ToolRegistry([]);
        var validator = new CreateAgentValidator(tools);
        var result = validator.TestValidate(
            new CreateAgentCommand("Support", "instr", ["not_a_tool"]));
        result.ShouldHaveValidationErrorFor(x => x.ToolNames);
    }

    [Fact]
    public async Task Handle_ShouldCreateAgent_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldCreateAgent_WhenInputIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();

        var result = await handler.Handle(
            new CreateAgentCommand("Support", "Help the user.", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        Assert.Equal("Support", result.Name);
        Assert.Equal("Help the user.", result.Instructions);
        Assert.True(result.IsActive);
        Assert.Contains(AgentToolNames.GetTenantInfo, result.ToolNames);
        Assert.NotEqual(Guid.Empty, result.AgentId);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNameAlreadyExists()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenNameAlreadyExists));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var command = new CreateAgentCommand("Support", "Help.", [AgentToolNames.GetTenantInfo]);

        await handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Seed_ShouldCreateDefaultAgent_ForDevTenant()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Seed_ShouldCreateDefaultAgent_ForDevTenant));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var list = scope.ServiceProvider.GetRequiredService<ListAgentsHandler>();

        var page = await list.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        var seed = Assert.Single(page.Data);
        Assert.True(seed.IsDefault);
        Assert.True(seed.IsActive);
        Assert.Equal(AgentSystemPrompt.Text, seed.Instructions);
        Assert.Equal(AgentToolNames.DefaultSeed, seed.ToolNames);
    }

    [Fact]
    public async Task CreateTenant_ShouldSeedDefaultAgent()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(CreateTenant_ShouldSeedDefaultAgent));

        using var scope = provider.CreateScope();
        var createTenant = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();
        var created = await createTenant.Handle(
            new CreateTenantCommand("acme", "Acme", null, TenantIsolationMode.SharedDb),
            CancellationToken.None);

        TestServiceFactory.SetTenant(scope.ServiceProvider, created.TenantId);
        var list = scope.ServiceProvider.GetRequiredService<ListAgentsHandler>();
        var page = await list.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        Assert.True(page.Data[0].IsDefault);
        Assert.Equal(AgentSystemPrompt.Text, page.Data[0].Instructions);
    }

    [Fact]
    public async Task Post_ShouldReturn409_WhenNameIsDuplicate()
    {
        await using var factory = EnableAiFactory();
        using var client = await CreateAuthenticatedClientAsync(factory);

        var body = new { name = "Dup", instructions = "x", toolNames = new[] { AgentToolNames.GetTenantInfo } };
        var first = await client.PostAsJsonAsync("/api/v1/ai/agents", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/ai/agents", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Business rule violation", problem?.Title);
    }

    [Fact]
    public async Task Post_ShouldReturn400_WhenToolNameIsUnknown()
    {
        await using var factory = EnableAiFactory();
        using var client = await CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = "Bad",
            instructions = "x",
            toolNames = new[] { "nope" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", problem?.Title);
    }

    [Fact]
    public async Task Agents_ShouldReturn404_WhenEnableAiIsFalse()
    {
        await using var factory = TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "false";
            settings["Seed:AdminPassword"] = TestPassword;
        });
        using var client = await CreateAuthenticatedClientAsync(factory);

        var get = await client.GetAsync("/api/v1/ai/agents");
        var post = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = "A",
            instructions = "x",
            toolNames = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        var problem = await get.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Feature disabled", problem?.Title);
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenNotAuthenticated()
    {
        await using var factory = EnableAiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var response = await client.GetAsync("/api/v1/ai/agents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenCallerLacksReadPermission()
    {
        await using var factory = EnableAiFactory();
        using var client = await CreateRegisteredUserClientAsync(factory);

        var response = await client.GetAsync("/api/v1/ai/agents");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ShouldReturn403_WhenCallerLacksManagePermission()
    {
        await using var factory = EnableAiFactory();
        using var client = await CreateRegisteredUserClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/ai/agents", new
        {
            name = "Nope",
            instructions = "x",
            toolNames = new[] { AgentToolNames.GetTenantInfo }
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static WebApplicationFactory<Program> EnableAiFactory() =>
        TestWebApplicationFactory.Create(settings =>
        {
            settings["FeatureFlags:EnableAI"] = "true";
            settings["Seed:AdminPassword"] = TestPassword;
        });

    internal static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email = "admin@producttemplate.com",
            password = TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenOutput>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<HttpClient> CreateRegisteredUserClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "dev");

        var email = $"plain-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email,
            password = TestPassword,
            firstName = "Plain",
            lastName = "User"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new
        {
            email,
            password = TestPassword
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenOutput>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}

public sealed class ListAgentsTests
{
    [Fact]
    public async Task Handle_ShouldReturnTenantAgents_OrderedByCreatedAtDesc()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldReturnTenantAgents_OrderedByCreatedAtDesc));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var list = scope.ServiceProvider.GetRequiredService<ListAgentsHandler>();

        await create.Handle(
            new CreateAgentCommand("Second", "two", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        var page = await list.Handle(new ListAgentsQuery(), CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(20, page.PageSize);
        Assert.Equal("Second", page.Data[0].Name);
    }
}

public sealed class GetAgentTests
{
    [Fact]
    public async Task Handle_ShouldThrow_WhenAgentDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenAgentDoesNotExist));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var handler = scope.ServiceProvider.GetRequiredService<GetAgentHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAgentQuery(Guid.NewGuid()), CancellationToken.None));
    }
}

public sealed class UpdateAgentTests
{
    [Fact]
    public async Task Handle_ShouldMutateSameRow_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldMutateSameRow_WhenInputIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var update = scope.ServiceProvider.GetRequiredService<UpdateAgentHandler>();
        var get = scope.ServiceProvider.GetRequiredService<GetAgentHandler>();

        var created = await create.Handle(
            new CreateAgentCommand("Support", "old", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        var updated = await update.Handle(
            new UpdateAgentCommand(
                created.AgentId,
                "Support desk",
                "new instructions",
                [AgentToolNames.GetUsersSummary]),
            CancellationToken.None);

        var reloaded = await get.Handle(new GetAgentQuery(created.AgentId), CancellationToken.None);

        Assert.Equal(created.AgentId, updated.AgentId);
        Assert.Equal("Support desk", reloaded.Name);
        Assert.Equal("new instructions", reloaded.Instructions);
        Assert.Equal([AgentToolNames.GetUsersSummary], reloaded.ToolNames);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenAgentDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenAgentDoesNotExist));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateAgentHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new UpdateAgentCommand(Guid.NewGuid(), "n", "i", [AgentToolNames.GetTenantInfo]),
                CancellationToken.None));
    }
}

public sealed class DeactivateAgentTests
{
    [Fact]
    public async Task Handle_ShouldDeactivate_WhenNotLastActive()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldDeactivate_WhenNotLastActive));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var create = scope.ServiceProvider.GetRequiredService<CreateAgentHandler>();
        var deactivate = scope.ServiceProvider.GetRequiredService<DeactivateAgentHandler>();
        var get = scope.ServiceProvider.GetRequiredService<GetAgentHandler>();

        var extra = await create.Handle(
            new CreateAgentCommand("Extra", "x", [AgentToolNames.GetTenantInfo]),
            CancellationToken.None);

        await deactivate.Handle(new DeactivateAgentCommand(extra.AgentId), CancellationToken.None);
        var reloaded = await get.Handle(new GetAgentQuery(extra.AgentId), CancellationToken.None);

        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenLastActiveAgent()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenLastActiveAgent));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var list = scope.ServiceProvider.GetRequiredService<ListAgentsHandler>();
        var deactivate = scope.ServiceProvider.GetRequiredService<DeactivateAgentHandler>();

        var page = await list.Handle(new ListAgentsQuery(), CancellationToken.None);
        var seed = Assert.Single(page.Data);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            deactivate.Handle(new DeactivateAgentCommand(seed.AgentId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenAgentDoesNotExist()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenAgentDoesNotExist));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantTestDefaults.DevelopmentTenantId);
        var handler = scope.ServiceProvider.GetRequiredService<DeactivateAgentHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeactivateAgentCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
