using System.Reflection;
using NetArchTest.Rules;

namespace ArchitectureTests;

public sealed class SliceStructureTests
{
    private static readonly Assembly AppAssembly = typeof(App.Features.Identity.RegisterUserHandler).Assembly;

    [Fact]
    public void Kernel_ShouldNotReference_Features()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .HaveName("Entity")
            .Or().HaveName("AggregateRoot")
            .Or().HaveName("Email")
            .Or().HaveName("BusinessRuleException")
            .Or().HaveName("NotFoundException")
            .ShouldNot()
            .HaveDependencyOn("App.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Platform_ShouldNotReference_Features()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .HaveName("ValidationBehavior")
            .Or().HaveName("PlatformExtensions")
            .Or().HaveName("EndpointRegistration")
            .Or().HaveName("TenantContext")
            .Or().HaveName("TenantContextBehavior")
            .Or().HaveName("Pbkdf2PasswordHasher")
            .ShouldNot()
            .HaveDependencyOn("App.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_ShouldNotReference_Features()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .HaveName("AppDbContext")
            .Or().HaveName("InfrastructureExtensions")
            .ShouldNot()
            .HaveDependencyOn("App.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Slices_ShouldImplement_IEndpoint_WhenTheyExposeHttp()
    {
        var endpointTypes = AppAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && typeof(App.Shared.IEndpoint).IsAssignableFrom(t));

        Assert.Contains(typeof(App.Features.Identity.RegisterUserEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.LoginEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.RefreshTokenEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.GetAuthProvidersEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.ExternalLoginEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.GetUserEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.ListUsersEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.UpdateUserEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.DeleteUserEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.ConfirmEmailEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Identity.GetUserRolesEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.CreateRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.ListRolesEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.GetRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.UpdateRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.DeleteRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.CreatePermissionEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.ListPermissionsEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.UpdatePermissionEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.DeletePermissionEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.AssignPermissionToRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.RevokePermissionFromRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.AssignUserToRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.GetUserAssignmentsEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Authorization.RevokeUserFromRoleEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Tenants.CreateTenantEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Tenants.ListTenantsEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Tenants.GetTenantEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Tenants.UpdateTenantEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Tenants.DeactivateTenantEndpoint), endpointTypes);
        Assert.Contains(typeof(App.Features.Ai.ChatAiEndpoint), endpointTypes);
    }
}
