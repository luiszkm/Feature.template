using App.Features.Ai;
using App.Shared;
using App.Tests.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests.Ai;

public sealed class ChatAiHandlerTests
{
    private static readonly Guid TenantId = TenantTestDefaults.DevelopmentTenantId;

    [Fact]
    public void Validator_ShouldFail_WhenMessageIsEmpty()
    {
        var validator = new ChatAiValidator();
        var result = validator.TestValidate(new ChatAiCommand(""));
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public async Task Handle_ShouldReturnReply_WhenMessageIsValid()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldReturnReply_WhenMessageIsValid));

        using var scope = provider.CreateScope();
        TestServiceFactory.SetTenant(scope.ServiceProvider, TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        var result = await handler.Handle(new ChatAiCommand("hello"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Reply));
        Assert.True(result.IterationsUsed > 0);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTenantIsMissing()
    {
        var provider = TestServiceFactory.CreateWithAi(nameof(Handle_ShouldThrow_WhenTenantIsMissing));

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ChatAiHandler>();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new ChatAiCommand("hello"), CancellationToken.None));
    }
}
