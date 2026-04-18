using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageSummary;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.AIUsageLogs;

public class GetAIUsageSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCalculateUsdFromTokenRates_NotFromChargedTokens()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var studentRoleId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var logs = new List<AIUsageLog>
        {
            new()
            {
                UsageLogId = Guid.NewGuid(),
                UserId = studentId,
                ConfigId = configId,
                AccessTierUsed = AIAccessTier.Paid,
                UsageType = AIUsageType.Assistant,
                ProviderName = "Mistral",
                Model = "mistral-small-2506",
                InputTokens = 1000,
                OutputTokens = 500,
                TotalTokens = 1500,
                ChargedTokens = 7m,
                CreatedAt = DateTime.UtcNow
            }
        };

        var configs = new List<AIProviderConfig>
        {
            new()
            {
                ConfigId = configId,
                ConfigJson = "{\"InputCostPer1M\":0.2,\"OutputCostPer1M\":0.6}"
            }
        };

        var users = new List<User>
        {
            new()
            {
                UserId = studentId,
                RoleId = studentRoleId,
                Role = new Role { RoleId = studentRoleId, RoleName = "Student" }
            }
        };

        var policies = new List<SystemRuntimePolicy>
        {
            new()
            {
                SystemRuntimePolicyId = Guid.NewGuid(),
                PolicyKey = "token_pricing_policy",
                ConfigJson = "{\"usdPerToken\":0.004}",
                IsActive = true
            }
        };

        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(x => x.AIUsageLogs).Returns(logs.BuildMockDbSet().Object);
        mockContext.Setup(x => x.AIProviderConfigs).Returns(configs.BuildMockDbSet().Object);
        mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        mockContext.Setup(x => x.SystemRuntimePolicies).Returns(policies.BuildMockDbSet().Object);

        var handler = new GetAIUsageSummaryQueryHandler(mockContext.Object);

        // Act
        var result = await handler.Handle(new GetAIUsageSummaryQuery
        {
            IncludeProviderModelBreakdown = true
        }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);

        var item = result.Value!.Single();
        item.TotalChargedTokens.Should().Be(7m);
        item.TotalRawChargedTokens.Should().Be(0.0005m);
        item.TotalCostUsd.Should().Be(0.0005m);
        item.TotalRevenueUsd.Should().Be(0.028m);
        item.TotalRawRevenueUsd.Should().Be(0.000002m);
        item.TotalProfitUsd.Should().Be(0.0275m);
        item.TotalRawProfitUsd.Should().Be(-0.000498m);
    }
}
