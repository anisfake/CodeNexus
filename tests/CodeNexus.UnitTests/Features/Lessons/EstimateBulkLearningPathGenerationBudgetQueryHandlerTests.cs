using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Lessons.Queries.EstimateBulkLearningPathGenerationBudget;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Lessons;

public class EstimateBulkLearningPathGenerationBudgetQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly EstimateBulkLearningPathGenerationBudgetQueryHandler _handler;

    public EstimateBulkLearningPathGenerationBudgetQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new EstimateBulkLearningPathGenerationBudgetQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_StudentPaidFlowAndInsufficientBalance_ReturnsEstimateAsNotEnough()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var student = new User
        {
            UserId = userId,
            RoleId = studentRole.RoleId,
            Role = studentRole,
            TokenBalance = 5m
        };

        var paidContentConfig = new AIProviderConfig
        {
            ConfigId = Guid.NewGuid(),
            AccessTier = AIAccessTier.Paid,
            UsageType = AIUsageType.ContentGeneration,
            IsActive = true,
            LastUpdated = DateTime.UtcNow,
            ConfigJson = """
                         {
                           "MaxTokens":"900",
                           "InputCostPer1M":"0.2",
                           "OutputCostPer1M":"0.6"
                         }
                         """
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new[] { paidContentConfig }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(
            new EstimateBulkLearningPathGenerationBudgetQuery(PendingLessonCount: 4, PendingQuizCount: 5),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsValidationApplied.Should().BeTrue();
        result.Value.IsEnoughTokenBalance.Should().BeFalse();
        result.Value.EstimatedRequiredTokens.Should().Be(10m);
        result.Value.CurrentTokenBalance.Should().Be(5m);
        result.Value.EstimatedAiCalls.Should().Be(9);
    }

    [Fact]
    public async Task Handle_StudentWithZeroBalance_SkipsValidation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var student = new User
        {
            UserId = userId,
            RoleId = studentRole.RoleId,
            Role = studentRole,
            TokenBalance = 0m
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new List<AIProviderConfig>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(
            new EstimateBulkLearningPathGenerationBudgetQuery(PendingLessonCount: 10, PendingQuizCount: 20),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsValidationApplied.Should().BeFalse();
        result.Value.IsEnoughTokenBalance.Should().BeTrue();
        result.Value.EstimatedRequiredTokens.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_Mentor_SkipsValidation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };
        var mentor = new User
        {
            UserId = userId,
            RoleId = mentorRole.RoleId,
            Role = mentorRole,
            TokenBalance = 500m
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new List<AIProviderConfig>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(
            new EstimateBulkLearningPathGenerationBudgetQuery(PendingLessonCount: 3, PendingQuizCount: 3),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsValidationApplied.Should().BeFalse();
        result.Value.IsEnoughTokenBalance.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StudentPaidFlowWithoutPaidConfig_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var student = new User
        {
            UserId = userId,
            RoleId = studentRole.RoleId,
            Role = studentRole,
            TokenBalance = 100m
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new List<AIProviderConfig>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(
            new EstimateBulkLearningPathGenerationBudgetQuery(PendingLessonCount: 2, PendingQuizCount: 2),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PAID_AI_CONFIG_NOT_FOUND");
    }
}
