using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIAccessPolicy;
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
    private readonly PlanUsageLimitService _service;

    public PlanUsageLimitServiceTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _service = new PlanUsageLimitService(_mockContext.Object);
    }

    [Fact]
    public async Task CheckLearningPathCreationAllowedAsync_ShouldRespectRuntimePolicyLimit()
    {
        var userId = Guid.NewGuid();

        SeedUsers(new[]
        {
            new User { UserId = userId, TokenBalance = 0m, Role = new Role { RoleName = "Student" } }
        });
        SeedFreeUsagePolicy(learningPathLimit: 2, tutorLimit: 300, focusLimit: 300);

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(Enumerable.Range(1, 2).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.LearningPathCreation,
            CreatedAt = DateTime.UtcNow.AddDays(-i + 1)
        }).BuildMockDbSet().Object);

        var result = await _service.CheckLearningPathCreationAllowedAsync(userId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("LEARNING_PATH_LIMIT_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task CheckLearningPathCreationAllowedAsync_PaidUser_ShouldBypassLimit()
    {
        var userId = Guid.NewGuid();
        SeedUsers(new[]
        {
            new User { UserId = userId, TokenBalance = 5000m, Role = new Role { RoleName = "Student" } }
        });
        SeedFreeUsagePolicy(learningPathLimit: 1, tutorLimit: 1, focusLimit: 1);

        var usageLogs = Enumerable.Range(1, 999).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.LearningPathCreation,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(usageLogs.BuildMockDbSet().Object);

        var result = await _service.CheckLearningPathCreationAllowedAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckTutorMessageAllowedAsync_FreeUserWithConfiguredLimit_ShouldFail()
    {
        var userId = Guid.NewGuid();
        SeedUsers(new[]
        {
            new User { UserId = userId, TokenBalance = 0m, Role = new Role { RoleName = "Student" } }
        });
        SeedFreeUsagePolicy(learningPathLimit: 3, tutorLimit: 5, focusLimit: 300);

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(Enumerable.Range(1, 5).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.TutorMessages,
            CreatedAt = DateTime.UtcNow
        }).BuildMockDbSet().Object);

        var result = await _service.CheckTutorMessageAllowedAsync(userId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TUTOR_MESSAGE_LIMIT_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task CheckFocusSessionReviewAllowedAsync_FreeUserBelowLimit_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        SeedUsers(new[]
        {
            new User { UserId = userId, TokenBalance = 0m, Role = new Role { RoleName = "Student" } }
        });
        SeedFreeUsagePolicy(learningPathLimit: 3, tutorLimit: 300, focusLimit: 10);

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(Enumerable.Range(1, 9).Select(i => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.FocusSessionReview,
            CreatedAt = DateTime.UtcNow
        }).BuildMockDbSet().Object);

        var result = await _service.CheckFocusSessionReviewAllowedAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckLearningPathCreationAllowedAsync_Mentor_ShouldBypassLimit()
    {
        var userId = Guid.NewGuid();
        SeedUsers(new[]
        {
            new User
            {
                UserId = userId,
                TokenBalance = 0m,
                Role = new Role { RoleName = "Mentor" }
            }
        });
        SeedFreeUsagePolicy(learningPathLimit: 1, tutorLimit: 1, focusLimit: 1);

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(new List<FeatureUsageLog>().BuildMockDbSet().Object);

        var result = await _service.CheckLearningPathCreationAllowedAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private void SeedUsers(IEnumerable<User> users)
    {
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
    }

    private void SeedFreeUsagePolicy(int learningPathLimit, int tutorLimit, int focusLimit)
    {
        var policy = new SystemRuntimePolicy
        {
            SystemRuntimePolicyId = Guid.NewGuid(),
            PolicyKey = FreeUsagePolicyConstants.PolicyKey,
            IsActive = true,
            ConfigJson = $"{{\"{FreeUsagePolicyConstants.LearningPathMonthlyLimitConfigKey}\":{learningPathLimit},\"{FreeUsagePolicyConstants.TutorMessagesMonthlyLimitConfigKey}\":{tutorLimit},\"{FreeUsagePolicyConstants.FocusSessionReviewMonthlyLimitConfigKey}\":{focusLimit}}}"
        };

        _mockContext.Setup(x => x.SystemRuntimePolicies)
            .Returns(new List<SystemRuntimePolicy> { policy }.BuildMockDbSet().Object);
    }
}

