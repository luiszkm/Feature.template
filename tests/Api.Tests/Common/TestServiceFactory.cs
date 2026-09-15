using Api.Features.Ai;
using Api.Features.Authorization;
using Api.Features.Identity;
using Api.Features.Tenants;
using Api.Host.Security;
using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Tests.Common;

public static class TestServiceFactory
{
    private static void AddCoreServices(IServiceCollection services, string databaseName)
    {
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddIdentityModule();
        services.AddAuthorizationModule();
        services.AddTenantsModule();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Llm:Provider"] = LlmProviders.OpenRouter,
                ["Ai:Llm:ApiKey"] = "",
                ["Ai:Llm:Model"] = "stub",
                ["FeatureFlags:EnableAI"] = "true"
            })
            .Build());
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IHostEnvironment>(_ => new TestHostEnvironment());
        services.AddHttpContextAccessor();
        services.Configure<JwtSettings>(options =>
        {
            options.Secret = "test-secret-key-minimum-32-characters-long";
            options.Issuer = "test";
            options.Audience = "test";
            options.ExpirationMinutes = 60;
            options.RefreshTokenExpirationDays = 30;
        });
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IServiceProvider BuildProvider(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        SeedTenants(provider);
        return provider;
    }

    private static void SeedTenants(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        EnsureTenant(
            db,
            WellKnownTenants.Public,
            "public",
            "Public Tenant");
        EnsureTenant(
            db,
            WellKnownTenants.Development,
            "dev",
            "Development Tenant");

        EnsureDefaultAgent(db, WellKnownTenants.Public);
        EnsureDefaultAgent(db, WellKnownTenants.Development);

        db.SaveChanges();
    }

    private static void EnsureTenant(AppDbContext db, Guid id, string key, string displayName)
    {
        if (db.Set<Tenant>().Any(t => t.Id == id))
            return;

        db.Set<Tenant>().Add(
            Tenant.CreateWithId(id, key, displayName, null, TenantIsolationMode.SharedDb));
    }

    private static void EnsureDefaultAgent(AppDbContext db, Guid tenantId)
    {
        if (db.Set<Agent>().IgnoreQueryFilters().Any(agent => agent.TenantId == tenantId && agent.IsDefault))
            return;

        db.Set<Agent>().Add(
            Agent.Create(tenantId, "Default", AgentSystemPrompt.Text, AgentToolNames.DefaultSeed, isDefault: true));
    }

    public static IServiceProvider Create(string databaseName)
    {
        var services = new ServiceCollection();
        AddCoreServices(services, databaseName);
        services.AddScoped<RegisterUserHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithLogin(string databaseName)
    {
        var services = new ServiceCollection();
        AddCoreServices(services, databaseName);
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithAuth(string databaseName)
    {
        var services = new ServiceCollection();
        AddCoreServices(services, databaseName);
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithAuthorization(string databaseName)
    {
        var services = new ServiceCollection();
        AddCoreServices(services, databaseName);
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<CreateRoleHandler>();
        services.AddScoped<ListRolesHandler>();
        services.AddScoped<GetRoleHandler>();
        services.AddScoped<UpdateRoleHandler>();
        services.AddScoped<DeleteRoleHandler>();
        services.AddScoped<CreatePermissionHandler>();
        services.AddScoped<ListPermissionsHandler>();
        services.AddScoped<UpdatePermissionHandler>();
        services.AddScoped<DeletePermissionHandler>();
        services.AddScoped<AssignPermissionToRoleHandler>();
        services.AddScoped<RevokePermissionFromRoleHandler>();
        services.AddScoped<AssignUserToRoleHandler>();
        services.AddScoped<GetUserAssignmentsHandler>();
        services.AddScoped<RevokeUserFromRoleHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithTenants(string databaseName)
    {
        var services = new ServiceCollection();
        AddCoreServices(services, databaseName);
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<ListTenantsHandler>();
        services.AddScoped<GetTenantHandler>();
        services.AddScoped<UpdateTenantHandler>();
        services.AddScoped<DeactivateTenantHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithIdentityManagement(string databaseName)
    {
        var services = new ServiceCollection();
        AddCoreServices(services, databaseName);
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<DeleteUserHandler>();
        services.AddScoped<GetUserRolesHandler>();
        services.AddScoped<CreateRoleHandler>();
        services.AddScoped<AssignUserToRoleHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithAi(string databaseName, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        AddCoreServices(services, databaseName);
        services.AddPlatform();
        services.AddAiModule();
        services.AddScoped<ChatAiHandler>();
        services.AddScoped<CreateAgentHandler>();
        services.AddScoped<ListAgentsHandler>();
        services.AddScoped<GetAgentHandler>();
        services.AddScoped<UpdateAgentHandler>();
        services.AddScoped<DeactivateAgentHandler>();
        services.AddScoped<CreateAgentFileHandler>();
        services.AddScoped<ListAgentFilesHandler>();
        services.AddScoped<GetAgentFileHandler>();
        services.AddScoped<DeleteAgentFileHandler>();
        services.AddScoped<CreateTenantHandler>();
        configure?.Invoke(services);
        return BuildProvider(services);
    }

    public static void SetTenant(IServiceProvider provider, Guid tenantId, string? tenantKey = null)
    {
        var context = provider.GetRequiredService<ITenantContext>();
        if (context is TenantContext mutable)
            mutable.SetTenant(tenantId, tenantKey);
    }

    public static async Task<User> SeedUserAsync(
        IServiceProvider provider,
        Guid tenantId,
        RegisterUserCommand? command = null)
    {
        using var scope = provider.CreateScope();
        SetTenant(scope.ServiceProvider, tenantId);

        var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        command ??= UserBuilder.ValidCommand();
        await registerHandler.Handle(command, CancellationToken.None);

        return await userRepository.GetByEmailAsync(command.Email, CancellationToken.None)
            ?? throw new InvalidOperationException("User was not created.");
    }

    public static async Task<(User User, RoleOutput AdminRole)> SeedUserWithAdminRoleAsync(
        IServiceProvider provider,
        Guid tenantId)
    {
        var user = await SeedUserAsync(provider, tenantId);

        using var scope = provider.CreateScope();
        SetTenant(scope.ServiceProvider, tenantId);

        var createRoleHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var assignHandler = scope.ServiceProvider.GetRequiredService<AssignUserToRoleHandler>();

        var adminRole = await createRoleHandler.Handle(
            new CreateRoleCommand("Admin", "Administrator role"),
            CancellationToken.None);

        await assignHandler.Handle(
            new AssignUserToRoleCommand(user.Id, adminRole.Id),
            CancellationToken.None);

        return (user, adminRole);
    }
}

public static class UserBuilder
{
    public static RegisterUserCommand ValidCommand() =>
        new("user@example.com", "Password1!", "John", "Doe");

    public static LoginCommand ValidLoginCommand() =>
        new("user@example.com", "Password1!");
}

public static class TenantTestDefaults
{
    public static readonly Guid DevelopmentTenantId = WellKnownTenants.Development;
}
