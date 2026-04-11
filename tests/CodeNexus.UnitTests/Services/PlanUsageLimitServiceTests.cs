using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Services;

public class PlanUsageLimitServiceTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly SubscriptionAccessService _subscriptionAccessService;
    private readonly PlanUsageLimitService _service;

    public PlanUsageLimitServiceTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _subscriptionAccessService = new SubscriptionAccessService(_mockContext.Object);
        _service = new PlanUsageLimitService(_mockContext.Object, _subscriptionAccessService);
    }

    [Fact]
    public async Task CheckLearningPathCreationAllowedAsync_FreeUserWithFourPaths_ShouldFail()
    {
        var userId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();

        SeedPlansAndUsers(
            new[]
            {
                new SubscriptionPlan { SubscriptionPlanId = freePlanId, PlanType = SubscriptionPlanType.Free, Name = "Free", IsActive = true }
            },
            new[]
            {
                new User { UserId = userId, SubscriptionPlanId = freePlanId, PlanExpiresAt = null }
            });

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(Enumerable.Range(1, 4).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.LearningPathCreation,
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).BuildMockDbSet().Object);

        var result = await _service.CheckLearningPathCreationAllowedAsync(userId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("LEARNING_PATH_LIMIT_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task CheckLearningPathCreationAllowedAsync_StandardUserWithNinePathsThisMonth_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var standardPlanId = Guid.NewGuid();

        SeedPlansAndUsers(
            new[]
            {
                new SubscriptionPlan { SubscriptionPlanId = standardPlanId, PlanType = SubscriptionPlanType.Standard, Name = "Standard", IsActive = true }
            },
            new[]
            {
                new User { UserId = userId, SubscriptionPlanId = standardPlanId, PlanExpiresAt = DateTime.UtcNow.AddDays(10) }
            });

        var usageLogs = Enumerable.Range(1, 9).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.LearningPathCreation,
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();

        usageLogs.Add(new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.LearningPathCreation,
            CreatedAt = DateTime.UtcNow.AddMonths(-2)
        });

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(usageLogs.BuildMockDbSet().Object);

        var result = await _service.CheckLearningPathCreationAllowedAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckTutorMessageAllowedAsync_FreeUserWithThirtyMessagesToday_ShouldFail()
    {
        var userId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedPlansAndUsers(
            new[]
            {
                new SubscriptionPlan { SubscriptionPlanId = freePlanId, PlanType = SubscriptionPlanType.Free, Name = "Free", IsActive = true }
            },
            new[]
            {
                new User { UserId = userId, SubscriptionPlanId = freePlanId, PlanExpiresAt = null }
            });

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(Enumerable.Range(1, 30).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.TutorMessages,
            CreatedAt = now
        }).BuildMockDbSet().Object);

        var result = await _service.CheckTutorMessageAllowedAsync(userId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TUTOR_MESSAGE_LIMIT_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task CheckTutorMessageAllowedAsync_ProUserBelowMonthlyLimit_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var proPlanId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedPlansAndUsers(
            new[]
            {
                new SubscriptionPlan { SubscriptionPlanId = proPlanId, PlanType = SubscriptionPlanType.Pro, Name = "Pro", IsActive = true }
            },
            new[]
            {
                new User { UserId = userId, SubscriptionPlanId = proPlanId, PlanExpiresAt = DateTime.UtcNow.AddDays(30) }
            });

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(Enumerable.Range(1, 1999).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.TutorMessages,
            CreatedAt = now
        }).BuildMockDbSet().Object);

        var result = await _service.CheckTutorMessageAllowedAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckLearningPathCreationAllowedAsync_Mentor_ShouldBypassPlanLimit()
    {
        var userId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();

        SeedPlansAndUsers(
            new[]
            {
                new SubscriptionPlan { SubscriptionPlanId = freePlanId, PlanType = SubscriptionPlanType.Free, Name = "Free", IsActive = true }
            },
            new[]
            {
                new User
                {
                    UserId = userId,
                    SubscriptionPlanId = freePlanId,
                    PlanExpiresAt = null,
                    Role = new Role { RoleName = "Mentor" }
                }
            });

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(new List<FeatureUsageLog>().BuildMockDbSet().Object);

        var result = await _service.CheckLearningPathCreationAllowedAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private void SeedPlansAndUsers(IEnumerable<SubscriptionPlan> plans, IEnumerable<User> users)
    {
        _mockContext.Setup(x => x.SubscriptionPlans).Returns(plans.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubscriptionPlanLimits).Returns(new List<SubscriptionPlanLimit>().BuildMockDbSet().Object);
    }
}
