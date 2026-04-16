using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.SystemRuntimePolicies.Commands.CreateSystemRuntimePolicy;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.SystemRuntimePolicies;

public class CreateSystemRuntimePolicyCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext = new();
    private readonly CreateSystemRuntimePolicyCommandHandler _handler;

    public CreateSystemRuntimePolicyCommandHandlerTests()
    {
        _handler = new CreateSystemRuntimePolicyCommandHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WhenPolicyKeyNotExists_ShouldCreateSuccessfully()
    {
        // Arrange
        var items = new List<SystemRuntimePolicy>();
        var dbSet = items.BuildMockDbSet();
        _mockContext.Setup(x => x.SystemRuntimePolicies).Returns(dbSet.Object);
        _mockContext.Setup(x => x.SystemRuntimePolicies.AddAsync(It.IsAny<SystemRuntimePolicy>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SystemRuntimePolicy>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SystemRuntimePolicy>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateSystemRuntimePolicyCommand(
            "token_pricing_policy",
            "Token pricing config",
            new Dictionary<string, object> { ["vndPerToken"] = 100 },
            true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("token_pricing_policy", result.Value.PolicyKey);
        Assert.True(result.Value.ConfigJson.ContainsKey("vndPerToken"));
    }

    [Fact]
    public async Task Handle_WhenPolicyKeyExists_ShouldReturnConflictFailure()
    {
        // Arrange
        var items = new List<SystemRuntimePolicy>
        {
            new()
            {
                SystemRuntimePolicyId = Guid.NewGuid(),
                PolicyKey = "token_pricing_policy",
                Description = "existing",
                ConfigJson = "{\"vndPerToken\":100}",
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var dbSet = items.BuildMockDbSet();
        _mockContext.Setup(x => x.SystemRuntimePolicies).Returns(dbSet.Object);

        var command = new CreateSystemRuntimePolicyCommand(
            "token_pricing_policy",
            "Token pricing config",
            new Dictionary<string, object> { ["vndPerToken"] = 120 },
            true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("POLICY_ALREADY_EXISTS", result.ErrorCode);
    }
}
