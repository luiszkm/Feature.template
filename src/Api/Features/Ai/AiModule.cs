using Api.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Features.Ai;

public static class AiModule
{
    public static IServiceCollection AddAiModule(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<AgentLoop>();
        services.AddSingleton<IAiUsageTracker, NoOpAiUsageTracker>();
        services.AddSingleton<ILlmService, StubLlmService>();

        services.AddScoped<ITool, GetUsersSummaryTool>();
        services.AddScoped<ITool, GetTenantInfoTool>();

        return services;
    }
}
