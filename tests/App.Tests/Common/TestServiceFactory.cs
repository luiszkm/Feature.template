using App.Features.Ai;
using App.Features.Authorization;
using App.Features.Identity;
using App.Features.Tenants;
using App.Host.Security;
using App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace App.Tests.Common;

public static class TestServiceFactory
{
    private static void AddCoreServices(IServiceCollection services, string databaseName)
    {
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddIdentityModule();
        services.AddAuthorizationModule();
        services.AddTenantsModule();
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
        services.AddScoped<App.Host.Security.IAuthenticationProviderFactory, App.Host.Security.AuthenticationProviderFactory>();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "App.Tests";
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

        db.SaveChanges();
    }

    private static void EnsureTenant(AppDbContext db, Guid id, string key, string displayName)
    {
        if (db.Set<Tenant>().Any(t => t.Id == id))
            return;

        db.Set<Tenant>().Add(
            Tenant.CreateWithId(id, key, displayName, null, TenantIsolationMode.SharedDb));
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
        services.AddScoped<ConfirmEmailHandler>();
        services.AddScoped<GetUserRolesHandler>();
        services.AddScoped<ExternalLoginHandler>();
        services.AddScoped<CreateRoleHandler>();
        services.AddScoped<AssignUserToRoleHandler>();
        return BuildProvider(services);
    }

    public static IServiceProvider CreateWithAi(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        AddCoreServices(services, databaseName);
        services.AddPlatform();
        services.AddAiModule();
        services.AddScoped<ChatAiHandler>();
        return BuildProvider(services);
    }

    public static void SetTenant(IServiceProvider provider, Guid tenantId, string? tenantKey = null)
    {
        var context = provider.GetRequiredService<ITenantContext>();
        if (context is TenantContext mutable)
            mutable.SetTenant(tenantId, tenantKey);
    }

    public static async Task<User> SeedConfirmedUserAsync(
        IServiceProvider provider,
        Guid tenantId,
        RegisterUserCommand? command = null)
    {
        using var scope = provider.CreateScope();
        SetTenant(scope.ServiceProvider, tenantId);

        var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        command ??= UserBuilder.ValidCommand();
        await registerHandler.Handle(command, CancellationToken.None);

        var user = await userRepository.GetByEmailAsync(command.Email, CancellationToken.None)
            ?? throw new InvalidOperationException("User was not created.");

        user.ConfirmEmail();
        await userRepository.UpdateAsync(user, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        return user;
    }

    public static async Task<(User User, RoleOutput AdminRole)> SeedUserWithAdminRoleAsync(
        IServiceProvider provider,
        Guid tenantId)
    {
        var user = await SeedConfirmedUserAsync(provider, tenantId);

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
