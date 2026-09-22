using Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Features.Ai;

public static class AiModule
{
    public static IServiceCollection AddAiModule(this IServiceCollection services)
    {
        services.AddOptions<LlmOptions>()
            .BindConfiguration(LlmOptions.SectionName);
        services.AddHostedService<LlmStartupGuard>();
        services.AddOptions<GuardrailOptions>().BindConfiguration(GuardrailOptions.SectionName);
        services.AddOptions<AiRateLimitOptions>().BindConfiguration(AiRateLimitOptions.SectionName);
        services.AddOptions<AiQuotaOptions>().BindConfiguration(AiQuotaOptions.SectionName);
        services.AddRateLimiter(options =>
            options.AddPolicy<string, AiRateLimitPolicy>(RateLimitPolicies.AiRateLimitPolicy));

        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAgentFileRepository, AgentFileRepository>();
        services.AddScoped<IAgentRuntimeContext, AgentRuntimeContext>();
        services.AddScoped<IDefaultAgentProvisioner, DefaultAgentProvisioner>();
        services.AddSingleton<ITenantQueryFilterConfigurator, AiTenantQueryFilters>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<AgentLoop>();
        services.AddSingleton<IContentGuard, AllowAllContentGuard>();
        services.AddScoped<AiQuota>();
        services.AddScoped<IAiUsageRepository, AiUsageRepository>();
        services.AddScoped<IAiUsageTracker, AiUsageTracker>();
        services.AddScoped<IModelComparisonRepository, ModelComparisonRepository>();
        services.AddMemoryCache();

        services.AddHttpClient(OpenRouterLlmService.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            client.BaseAddress = new Uri(LlmServiceResolver.EffectiveBaseUrl(options) + "/");
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
        });
        services.AddHttpClient(MicrosoftAgentFrameworkLlmService.HttpClientName);

        services.AddSingleton<ILlmService>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            if (LlmServiceResolver.UseStub(environment, options))
                return new StubLlmService();

            return string.Equals(
                LlmProviders.Normalize(options.Provider),
                LlmProviders.MicrosoftAgentFramework,
                StringComparison.Ordinal)
                ? ActivatorUtilities.CreateInstance<MicrosoftAgentFrameworkLlmService>(sp)
                : ActivatorUtilities.CreateInstance<OpenRouterLlmService>(sp);
        });

        services.AddSingleton<IModelCatalog>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            if (LlmServiceResolver.UseStub(environment, options))
                return ActivatorUtilities.CreateInstance<StubModelCatalog>(sp);

            return LlmServiceResolver.IsMicrosoftAgentFramework(environment, options)
                ? ActivatorUtilities.CreateInstance<ConfiguredModelCatalog>(sp)
                : ActivatorUtilities.CreateInstance<OpenRouterModelCatalog>(sp);
        });

        services.AddScoped<ITool, GetUsersSummaryTool>();
        services.AddScoped<ITool, GetTenantInfoTool>();
        services.AddScoped<ITool, ListAgentFilesTool>();
        services.AddScoped<ITool, ReadAgentFileTool>();

        return services;
    }

    public static AuthorizationOptions AddAiModulePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(SecurityPolicies.AiAgentsRead, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(AuthorizationClaimTypes.Permission, AiPermissions.AgentRead)));

        options.AddPolicy(SecurityPolicies.AiAgentsManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.HasClaim(AuthorizationClaimTypes.Permission, AiPermissions.AgentManage)));

        return options;
    }
}

internal sealed class AiTenantQueryFilters : ITenantQueryFilterConfigurator
{
    public void Configure(ModelBuilder modelBuilder, AppDbContext dbContext)
    {
        modelBuilder.Entity<Agent>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<AgentFile>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<AiUsageEntry>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);

        modelBuilder.Entity<ModelComparison>().HasQueryFilter(
            entity => dbContext.CurrentTenantId != null && entity.TenantId == dbContext.CurrentTenantId);
    }
}

internal sealed class DefaultAgentProvisioner(AppDbContext db) : IDefaultAgentProvisioner
{
    public async Task EnsureDefaultAgentAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Set<Agent>().IgnoreQueryFilters()
            .AnyAsync(agent => agent.TenantId == tenantId && agent.IsDefault, cancellationToken);

        if (exists)
            return;

        var agent = Agent.Create(
            tenantId,
            "Default",
            AgentSystemPrompt.Text,
            AgentToolNames.DefaultSeed,
            isDefault: true);

        await db.Set<Agent>().AddAsync(agent, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
