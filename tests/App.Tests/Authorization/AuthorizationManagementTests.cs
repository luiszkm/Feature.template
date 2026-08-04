using App.Features.Authorization;
using App.Features.Identity;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Authorization;

file static class AuthorizationTestDb
{
    public static string Name(string testMethod) => $"AuthMgmt_{testMethod}";
}

public sealed class GetRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldReturnRoleWithPermissions_WhenRoleExists()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldReturnRoleWithPermissions_WhenRoleExists)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createRoleHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var createPermissionHandler = scope.ServiceProvider.GetRequiredService<CreatePermissionHandler>();
        var assignHandler = scope.ServiceProvider.GetRequiredService<AssignPermissionToRoleHandler>();
        var getRoleHandler = scope.ServiceProvider.GetRequiredService<GetRoleHandler>();

        var role = await createRoleHandler.Handle(new CreateRoleCommand("Manager", "Managers"), CancellationToken.None);
        var permission = await createPermissionHandler.Handle(
            new CreatePermissionCommand("users.read", "Read users"),
            CancellationToken.None);
        await assignHandler.Handle(
            new AssignPermissionToRoleCommand(role.Id, permission.Id),
            CancellationToken.None);

        var result = await getRoleHandler.Handle(new GetRoleQuery(role.Id), CancellationToken.None);

        Assert.Equal("Manager", result.Name);
        Assert.Single(result.Permissions);
        Assert.Equal("users.read", result.Permissions[0].Name);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenRoleNotFound()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldThrow_WhenRoleNotFound)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<GetRoleHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetRoleQuery(Guid.NewGuid()), CancellationToken.None));
    }
}

public sealed class UpdateRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public void Validator_ShouldFail_WhenNameIsEmpty()
    {
        var validator = new UpdateRoleValidator();
        var result = validator.TestValidate(new UpdateRoleCommand(Guid.NewGuid(), "", "desc"));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Handle_ShouldUpdateRole_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldUpdateRole_WhenInputIsValid)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var updateHandler = scope.ServiceProvider.GetRequiredService<UpdateRoleHandler>();

        var role = await createHandler.Handle(new CreateRoleCommand("Manager", "Old"), CancellationToken.None);
        var result = await updateHandler.Handle(
            new UpdateRoleCommand(role.Id, "Senior Manager", "Updated"),
            CancellationToken.None);

        Assert.Equal("Senior Manager", result.Name);
        Assert.Equal("Updated", result.Description);
    }
}

public sealed class DeleteRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldDeleteRole_WhenRoleExists()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldDeleteRole_WhenRoleExists)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var deleteHandler = scope.ServiceProvider.GetRequiredService<DeleteRoleHandler>();
        var getRoleHandler = scope.ServiceProvider.GetRequiredService<GetRoleHandler>();

        var role = await createHandler.Handle(new CreateRoleCommand("Temp", "Temporary"), CancellationToken.None);
        await deleteHandler.Handle(new DeleteRoleCommand(role.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            getRoleHandler.Handle(new GetRoleQuery(role.Id), CancellationToken.None));
    }
}

public sealed class RevokePermissionFromRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldRevokePermission_WhenAssignmentExists()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldRevokePermission_WhenAssignmentExists)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createRoleHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var createPermissionHandler = scope.ServiceProvider.GetRequiredService<CreatePermissionHandler>();
        var assignHandler = scope.ServiceProvider.GetRequiredService<AssignPermissionToRoleHandler>();
        var revokeHandler = scope.ServiceProvider.GetRequiredService<RevokePermissionFromRoleHandler>();
        var getRoleHandler = scope.ServiceProvider.GetRequiredService<GetRoleHandler>();

        var role = await createRoleHandler.Handle(new CreateRoleCommand("Manager", "Managers"), CancellationToken.None);
        var permission = await createPermissionHandler.Handle(
            new CreatePermissionCommand("users.read", "Read users"),
            CancellationToken.None);
        await assignHandler.Handle(
            new AssignPermissionToRoleCommand(role.Id, permission.Id),
            CancellationToken.None);

        await revokeHandler.Handle(
            new RevokePermissionFromRoleCommand(role.Id, permission.Id),
            CancellationToken.None);

        var result = await getRoleHandler.Handle(new GetRoleQuery(role.Id), CancellationToken.None);
        Assert.Empty(result.Permissions);
    }
}

public sealed class ListPermissionsTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldReturnAllPermissions()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldReturnAllPermissions)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createHandler = scope.ServiceProvider.GetRequiredService<CreatePermissionHandler>();
        var listHandler = scope.ServiceProvider.GetRequiredService<ListPermissionsHandler>();

        await createHandler.Handle(new CreatePermissionCommand("users.read", "Read"), CancellationToken.None);
        await createHandler.Handle(new CreatePermissionCommand("users.manage", "Manage"), CancellationToken.None);

        var result = await listHandler.Handle(new ListPermissionsQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Data.Count);
    }
}

public sealed class UpdatePermissionTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldUpdatePermission_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldUpdatePermission_WhenInputIsValid)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createHandler = scope.ServiceProvider.GetRequiredService<CreatePermissionHandler>();
        var updateHandler = scope.ServiceProvider.GetRequiredService<UpdatePermissionHandler>();

        var permission = await createHandler.Handle(
            new CreatePermissionCommand("users.read", "Old"),
            CancellationToken.None);

        var result = await updateHandler.Handle(
            new UpdatePermissionCommand(permission.Id, "users.read.all", "Updated"),
            CancellationToken.None);

        Assert.Equal("users.read.all", result.Name);
        Assert.Equal("Updated", result.Description);
    }
}

public sealed class DeletePermissionTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldDeletePermission_WhenPermissionExists()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldDeletePermission_WhenPermissionExists)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createHandler = scope.ServiceProvider.GetRequiredService<CreatePermissionHandler>();
        var deleteHandler = scope.ServiceProvider.GetRequiredService<DeletePermissionHandler>();
        var listHandler = scope.ServiceProvider.GetRequiredService<ListPermissionsHandler>();

        var permission = await createHandler.Handle(
            new CreatePermissionCommand("temp.perm", "Temporary"),
            CancellationToken.None);

        await deleteHandler.Handle(new DeletePermissionCommand(permission.Id), CancellationToken.None);

        var result = await listHandler.Handle(new ListPermissionsQuery(), CancellationToken.None);
        Assert.Empty(result.Data);
    }
}

public sealed class GetUserAssignmentsTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldReturnAssignedRoles()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldReturnAssignedRoles)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var createRoleHandler = scope.ServiceProvider.GetRequiredService<CreateRoleHandler>();
        var assignHandler = scope.ServiceProvider.GetRequiredService<AssignUserToRoleHandler>();
        var getAssignmentsHandler = scope.ServiceProvider.GetRequiredService<GetUserAssignmentsHandler>();

        var role = await createRoleHandler.Handle(new CreateRoleCommand("User", "Standard user"), CancellationToken.None);
        await assignHandler.Handle(new AssignUserToRoleCommand(user.Id, role.Id), CancellationToken.None);

        var roles = await getAssignmentsHandler.Handle(new GetUserAssignmentsQuery(user.Id), CancellationToken.None);

        Assert.Single(roles);
        Assert.Equal("User", roles[0].Name);
    }
}

public sealed class RevokeUserFromRoleTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldRevokeAssignment_WhenUserHasRole()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldRevokeAssignment_WhenUserHasRole)));
        var (user, adminRole) = await TestServiceFactory.SeedUserWithAdminRoleAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var revokeHandler = scope.ServiceProvider.GetRequiredService<RevokeUserFromRoleHandler>();
        var getAssignmentsHandler = scope.ServiceProvider.GetRequiredService<GetUserAssignmentsHandler>();

        await revokeHandler.Handle(new RevokeUserFromRoleCommand(user.Id, adminRole.Id), CancellationToken.None);

        var roles = await getAssignmentsHandler.Handle(new GetUserAssignmentsQuery(user.Id), CancellationToken.None);
        Assert.Empty(roles);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenAssignmentNotFound()
    {
        var provider = TestServiceFactory.CreateWithAuthorization(AuthorizationTestDb.Name(nameof(Handle_ShouldThrow_WhenAssignmentNotFound)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var revokeHandler = scope.ServiceProvider.GetRequiredService<RevokeUserFromRoleHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            revokeHandler.Handle(new RevokeUserFromRoleCommand(user.Id, Guid.NewGuid()), CancellationToken.None));
    }
}
