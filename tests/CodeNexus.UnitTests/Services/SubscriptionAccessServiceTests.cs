using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Services;

public class SubscriptionAccessServiceTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly SubscriptionAccessService _service;

    public SubscriptionAccessServiceTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _service = new SubscriptionAccessService(_mockContext.Object);
    }

    [Fact]
    public async Task CanUsePersonalGoalsAsync_Mentor_ShouldReturnTrue()
    {
        var userId = Guid.NewGuid();
        _mockContext.Setup(x => x.Users).Returns(new List<User>
        {
            new()
            {
                UserId = userId,
                Role = new Role { RoleName = "Mentor" }
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubscriptionPlans).Returns(new List<SubscriptionPlan>
        {
            new()
            {
                SubscriptionPlanId = Guid.NewGuid(),
                PlanType = SubscriptionPlanType.Free,
                Name = "Free",
                IsActive = true
            }
        }.BuildMockDbSet().Object);

        var result = await _service.CanUsePersonalGoalsAsync(userId, CancellationToken.None);

        Assert.True(result);
    }
}

