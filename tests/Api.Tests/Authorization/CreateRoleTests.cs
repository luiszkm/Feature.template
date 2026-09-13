using App.Features.Authorization;
using App.Features.Identity;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Authorization;

public sealed class CreateRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public void Validator_ShouldFail_WhenNameIsEmpty()
    {
        var validator = new CreateRoleValidator();
        var result = validator.TestValidate(new CreateRoleCommand("", "desc"));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Handle_ShouldCreateRole_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(nameof(Handle_ShouldCreateRole_WhenInputIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();

        var result = await handler.Handle(new CreateRoleCommand("Manager", "Managers"), CancellationToken.None);

        Assert.Equal("Manager", result.Name);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenRoleNameAlreadyExists()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(nameof(Handle_ShouldThrow_WhenRoleNameAlreadyExists));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var command = new CreateRoleCommand("Manager", "Managers");

        await handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}

public sealed class AssignUserToRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldAssignRole_WhenUserAndRoleExist()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(nameof(Handle_ShouldAssignRole_WhenUserAndRoleExist));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createRoleHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var assignHandler = scope.ServiceProvider.GetRequiredService<AssignUserToRoleHandler>();
        var rolesProvider = scope.ServiceProvider.GetRequiredService<IUserRolesProvider>();

        var role = await createRoleHandler.Handle(new CreateRoleCommand("User", "Standard user"), CancellationToken.None);
        await assignHandler.Handle(new AssignUserToRoleCommand(user.Id, role.Id), CancellationToken.None);

        var rolesData = await rolesProvider.GetUserRolesAndPermissionsAsync(user.Id, CancellationToken.None);

        Assert.Contains("User", rolesData.Roles);
    }
}

public sealed class LoginRolesIntegrationTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Login_ShouldIncludeAssignedRoles_InAuthOutput()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(nameof(Login_ShouldIncludeAssignedRoles_InAuthOutput));
        await TestServiceFactory.SeedUserWithAdminRoleAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        var result = await loginHandler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None);

        Assert.Contains("Admin", result.User.Roles);
    }
}
