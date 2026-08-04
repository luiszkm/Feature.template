using App.Features.Authorization;
using App.Features.Identity;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Identity;

file static class IdentityTestDb
{
    public static string Name(string testMethod) => $"Identity_{testMethod}";
}

public sealed class GetUserTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldReturnUser_WhenUserExists()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(IdentityTestDb.Name(nameof(Handle_ShouldReturnUser_WhenUserExists)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<GetUserHandler>();

        var result = await handler.Handle(new GetUserQuery(user.Id), CancellationToken.None);

        Assert.Equal(user.Id, result.Id);
        Assert.Equal("user@example.com", result.Email);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserNotFound()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(IdentityTestDb.Name(nameof(Handle_ShouldThrow_WhenUserNotFound)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<GetUserHandler>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetUserQuery(Guid.NewGuid()), CancellationToken.None));
    }
}

public sealed class ListUsersTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldReturnAllUsers_InTenant()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(IdentityTestDb.Name(nameof(Handle_ShouldReturnAllUsers_InTenant)));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();

        await registerHandler.Handle(UserBuilder.ValidCommand(), CancellationToken.None);
        await registerHandler.Handle(
            UserBuilder.ValidCommand() with { Email = "other@example.com" },
            CancellationToken.None);

        var listHandler = scope.ServiceProvider.GetRequiredService<ListUsersHandler>();
        var result = await listHandler.Handle(new ListUsersQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.PageNumber);
    }
}

public sealed class UpdateUserTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public void Validator_ShouldFail_WhenFirstNameIsEmpty()
    {
        var validator = new UpdateUserValidator();
        var result = validator.TestValidate(new UpdateUserCommand(Guid.NewGuid(), "", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProfile_WhenInputIsValid()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(IdentityTestDb.Name(nameof(Handle_ShouldUpdateProfile_WhenInputIsValid)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateUserHandler>();

        var result = await handler.Handle(
            new UpdateUserCommand(user.Id, "Jane", "Smith"),
            CancellationToken.None);

        Assert.Equal("Jane", result.FirstName);
        Assert.Equal("Smith", result.LastName);
    }
}

public sealed class DeleteUserTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldDeactivateUser_WhenUserExists()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(IdentityTestDb.Name(nameof(Handle_ShouldDeactivateUser_WhenUserExists)));
        var user = await TestServiceFactory.SeedUserAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);

        var deleteHandler = scope.ServiceProvider.GetRequiredService<DeleteUserHandler>();
        await deleteHandler.Handle(new DeleteUserCommand(user.Id), CancellationToken.None);

        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            loginHandler.Handle(UserBuilder.ValidLoginCommand(), CancellationToken.None));
    }
}

public sealed class GetUserRolesTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public async Task Handle_ShouldReturnAssignedRoles()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            IdentityTestDb.Name($"{nameof(GetUserRolesTests)}_{nameof(Handle_ShouldReturnAssignedRoles)}"));
        var (user, adminRole) = await TestServiceFactory.SeedUserWithAdminRoleAsync(provider, TenantId);

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<GetUserRolesHandler>();

        var roles = await handler.Handle(new GetUserRolesQuery(user.Id), CancellationToken.None);

        Assert.Contains("Admin", roles);
    }
}
