using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIProfitOverview;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.AIUsageLogs;

public class GetAIProfitOverviewQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnCostSplitStudentRawAndBilledAmountsWithProfit()
    {
        // Arrange
        var freeConfigId = Guid.NewGuid();
        var paidMistralConfigId = Guid.NewGuid();
        var otherConfigId = Guid.NewGuid();

        var studentRoleId = Guid.NewGuid();
        var mentorRoleId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();

        var logs = new List<AIUsageLog>
        {
            new()
            {
                UsageLogId = Guid.NewGuid(),
                ConfigId = freeConfigId,
                AccessTierUsed = AIAccessTier.Free,
                ProviderName = "groq - free",
                InputTokens = 1000,
                OutputTokens = 500,
                TotalTokens = 1500,
                ChargedTokens = 1m,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                UsageLogId = Guid.NewGuid(),
                UserId = studentId,
                ConfigId = paidMistralConfigId,
                AccessTierUsed = AIAccessTier.Paid,
                ProviderName = "mistral - paid",
                InputTokens = 2000,
                OutputTokens = 1000,
                TotalTokens = 3000,
                ChargedTokens = 1m,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                UsageLogId = Guid.NewGuid(),
                UserId = mentorId,
                ConfigId = paidMistralConfigId,
                AccessTierUsed = AIAccessTier.Paid,
                ProviderName = "mistral - paid",
                InputTokens = 1000,
                OutputTokens = 1000,
                TotalTokens = 2000,
                ChargedTokens = 1m,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                UsageLogId = Guid.NewGuid(),
                UserId = studentId,
                ConfigId = otherConfigId,
                AccessTierUsed = AIAccessTier.Paid,
                ProviderName = "gemini - paid",
                InputTokens = 1000,
                OutputTokens = 0,
                TotalTokens = 1000,
                ChargedTokens = 1m,
                CreatedAt = DateTime.UtcNow
            }
        };

        var configs = new List<AIProviderConfig>
        {
            new() { ConfigId = freeConfigId, ConfigJson = "{\"InputCostPer1M\":0.2,\"OutputCostPer1M\":0.6}" },
            new() { ConfigId = paidMistralConfigId, ConfigJson = "{\"InputCostPer1M\":0.15,\"OutputCostPer1M\":0.6}" },
            new() { ConfigId = otherConfigId, ConfigJson = "{\"InputCostPer1M\":1.0,\"OutputCostPer1M\":0.0}" }
        };

        var users = new List<User>
        {
            new()
            {
                UserId = studentId,
                RoleId = studentRoleId,
                Role = new Role { RoleId = studentRoleId, RoleName = "Student" }
            },
            new()
            {
                UserId = mentorId,
                RoleId = mentorRoleId,
                Role = new Role { RoleId = mentorRoleId, RoleName = "Mentor" }
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

        var handler = new GetAIProfitOverviewQueryHandler(mockContext.Object);

        // Act
        var result = await handler.Handle(new GetAIProfitOverviewQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var item = result.Value!;
        item.SystemCostFreeUsd.Should().Be(0.0005m);
        item.SystemCostPaidUsd.Should().Be(0.00265m);
        item.SystemCostTotalUsd.Should().Be(0.00315m);
        item.StudentUsageCostUsd.Should().Be(0.0019m);
        item.StudentUsageRawUsd.Should().Be(0.0000076m);
        item.StudentBilledRevenueUsd.Should().Be(0.008m);
        item.StudentRevenueRawUsd.Should().Be(0.0000076m);
        item.ProfitUsd.Should().Be(-0.0031424m);
    }
}
