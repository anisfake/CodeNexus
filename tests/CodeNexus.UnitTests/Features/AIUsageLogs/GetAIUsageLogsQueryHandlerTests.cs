using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageLogs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.AIUsageLogs;

public class GetAIUsageLogsQueryHandlerTests
{
    [Fact]
    public async Task Handle_SortByCostUsd_ShouldUseCalculatedUsdInsteadOfChargedTokens()
    {
        // Arrange
        var highCostConfigId = Guid.NewGuid();
        var lowCostConfigId = Guid.NewGuid();

        var highCostLogId = Guid.NewGuid();
        var lowCostLogId = Guid.NewGuid();

        var logs = new List<AIUsageLog>
        {
            new()
            {
                UsageLogId = highCostLogId,
                ConfigId = highCostConfigId,
                AccessTierUsed = AIAccessTier.Paid,
                UsageType = AIUsageType.StructureGeneration,
                ProviderName = "Mistral",
                Model = "mistral-small-2506",
                InputTokens = 10000,
                OutputTokens = 0,
                TotalTokens = 10000,
                ChargedTokens = 1m,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                UsageLogId = lowCostLogId,
                ConfigId = lowCostConfigId,
                AccessTierUsed = AIAccessTier.Paid,
                UsageType = AIUsageType.StructureGeneration,
                ProviderName = "Mistral",
                Model = "mistral-small-2506",
                InputTokens = 10000,
                OutputTokens = 0,
                TotalTokens = 10000,
                ChargedTokens = 9m,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        var configs = new List<AIProviderConfig>
        {
            new()
            {
                ConfigId = highCostConfigId,
                ConfigJson = "{\"InputCostPer1M\":1.0,\"OutputCostPer1M\":1.0}"
            },
            new()
            {
                ConfigId = lowCostConfigId,
                ConfigJson = "{\"InputCostPer1M\":0.1,\"OutputCostPer1M\":0.1}"
            }
        };

        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(x => x.AIUsageLogs).Returns(logs.BuildMockDbSet().Object);
        mockContext.Setup(x => x.AIProviderConfigs).Returns(configs.BuildMockDbSet().Object);

        var handler = new GetAIUsageLogsQueryHandler(mockContext.Object);

        // Act
        var result = await handler.Handle(new GetAIUsageLogsQuery
        {
            SortBy = AIUsageLogSortBy.CostUsd,
            SortDescending = true,
            PageNumber = 1,
            PageSize = 10
        }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);

        var first = result.Value.Items[0];
        var second = result.Value.Items[1];

        first.UsageLogId.Should().Be(highCostLogId);
        first.CostUsd.Should().BeGreaterThan(second.CostUsd);
        first.CostUsd.Should().NotBe(first.ChargedTokens);
        second.CostUsd.Should().NotBe(second.ChargedTokens);
    }
}

