using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GenerateLearningPathSkeletonCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateLearningPathSkeletonCommandHandler _handler;

    public GenerateLearningPathSkeletonCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();
        
        _handler = new GenerateLearningPathSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object
        );
    }

    [Fact]
    public async Task Handle_WithInvalidSubjectId_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goalId, ComplexityLevel.Beginner, LanguageSelection.VietNamese);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subject)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GENERATION_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithInvalidGoalId_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goalId, ComplexityLevel.Intermediate, LanguageSelection.English);

        var subject = new Subject { SubjectId = subjectId, Name = "C#" };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subject);
        _mockContext.Setup(x => x.Goals.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CodeNexus.Domain.Entities.Goals)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GENERATION_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithAIGenerationFailure_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goalId, ComplexityLevel.Advanced, LanguageSelection.VietNamese);

        var subject = new Subject { SubjectId = subjectId, Name = "C#" };
        var goal = new CodeNexus.Domain.Entities.Goals 
        { 
            GoalId = goalId, 
            Title = "Master C#", 
            CreatedByUserId = userId,
            IsSystemDefined = false,
            IsActive = true
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subject);
        _mockContext.Setup(x => x.Goals.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(goal);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<LearningPathSkeletonDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ThrowsAsync(new InvalidOperationException("AI service error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GENERATION_FAILED", result.ErrorCode);
    }
}
