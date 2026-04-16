using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Payments;
using CodeNexus.Application.Features.TokenPackages.Queries.GetPublicTokenPricing;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.TokenPackages;

public class GetPublicTokenPricingQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext = new();
    private readonly GetPublicTokenPricingQueryHandler _handler;

    public GetPublicTokenPricingQueryHandlerTests()
    {
        _handler = new GetPublicTokenPricingQueryHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WhenPolicyMissing_ShouldReturnDefaults()
    {
        // Arrange
        var policies = new List<SystemRuntimePolicy>();
        _mockContext.Setup(x => x.SystemRuntimePolicies).Returns(policies.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(new GetPublicTokenPricingQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(TokenPricingConstants.DefaultCustomTopUpVndPerToken, result.Value.VndPerToken);
        Assert.Equal(TokenPricingConstants.DefaultMinCustomTopUpVnd, result.Value.MinimumTopUpVnd);
        Assert.Equal(TokenPricingConstants.DefaultMaxCustomTopUpVnd, result.Value.MaximumTopUpVnd);
    }

    [Fact]
    public async Task Handle_WhenPolicyExists_ShouldReturnConfiguredRate()
    {
        // Arrange
        var policies = new List<SystemRuntimePolicy>
        {
            new()
            {
                SystemRuntimePolicyId = Guid.NewGuid(),
                PolicyKey = TokenPricingConstants.TokenPricingPolicyKey,
                Description = "Token rate",
                ConfigJson = "{\"vndPerToken\":120,\"minCustomTopUpVnd\":20000,\"maxCustomTopUpVnd\":2500000}",
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            }
        };
        _mockContext.Setup(x => x.SystemRuntimePolicies).Returns(policies.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(new GetPublicTokenPricingQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(120m, result.Value.VndPerToken);
        Assert.Equal(8.3333m, result.Value.TokensPer1000Vnd);
        Assert.Equal(20000m, result.Value.MinimumTopUpVnd);
        Assert.Equal(2500000m, result.Value.MaximumTopUpVnd);
    }
}
