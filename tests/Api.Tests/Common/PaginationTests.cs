using Api.Features.Authorization;
using Api.Features.Identity;
using Api.Shared;
using Api.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Common;

public sealed class PaginationTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public void ListQueryValidator_ShouldFail_WhenPageSizeExceedsMax()
    {
        var validator = new ListUsersQueryValidator();
        var result = validator.TestValidate(new ListUsersQuery(PageSize: 101));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void ListQueryValidator_ShouldFail_WhenPageNumberIsZero()
    {
        var validator = new ListRolesQueryValidator();
        var result = validator.TestValidate(new ListRolesQuery(PageNumber: 0));
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Fact]
    public async Task ListUsers_ShouldUseDefaultPageSize_WhenNotSpecified()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            $"Pagination_{nameof(ListUsers_ShouldUseDefaultPageSize_WhenNotSpecified)}");

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ListUsersHandler>();

        var result = await handler.Handle(new ListUsersQuery(), CancellationToken.None);

        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.PageNumber);
    }

    [Fact]
    public async Task ListUsers_ShouldReturnSecondPage_WhenPageNumberIsTwo()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            $"Pagination_{nameof(ListUsers_ShouldReturnSecondPage_WhenPageNumberIsTwo)}");

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        var listHandler = scope.ServiceProvider.GetRequiredService<ListUsersHandler>();

        for (var i = 0; i < 25; i++)
        {
            await registerHandler.Handle(
                UserBuilder.ValidCommand() with { Email = $"user{i}@example.com" },
                CancellationToken.None);
        }

        var page1 = await listHandler.Handle(new ListUsersQuery(PageSize: 20), CancellationToken.None);
        var page2 = await listHandler.Handle(new ListUsersQuery(PageNumber: 2, PageSize: 20), CancellationToken.None);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Data.Count);
        Assert.Equal(5, page2.Data.Count);
        Assert.Equal(2, page2.PageNumber);
    }

    [Fact]
    public async Task ListUsers_ShouldFilterBySearchTerm()
    {
        var provider = TestServiceFactory.CreateWithIdentityManagement(
            $"Pagination_{nameof(ListUsers_ShouldFilterBySearchTerm)}");

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        var listHandler = scope.ServiceProvider.GetRequiredService<ListUsersHandler>();

        await registerHandler.Handle(UserBuilder.ValidCommand(), CancellationToken.None);
        await registerHandler.Handle(
            UserBuilder.ValidCommand() with { Email = "alice@example.com", FirstName = "Alice", LastName = "Smith" },
            CancellationToken.None);

        var result = await listHandler.Handle(
            new ListUsersQuery(SearchTerm: "Alice"),
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alice", result.Data[0].FirstName);
    }
}
