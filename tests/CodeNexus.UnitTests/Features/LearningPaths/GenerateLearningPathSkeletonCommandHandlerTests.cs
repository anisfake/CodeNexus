using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GenerateLearningPathSkeletonCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ITimelineCalculationService> _mockTimelineCalculationService;
    private readonly Mock<ISubscriptionAccessService> _mockSubscriptionAccessService;
    private readonly Mock<IPlanUsageLimitService> _mockPlanUsageLimitService;
    private readonly GenerateLearningPathSkeletonCommandHandler _handler;

    public GenerateLearningPathSkeletonCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockTimelineCalculationService = new Mock<ITimelineCalculationService>();
        _mockSubscriptionAccessService = new Mock<ISubscriptionAccessService>();
        _mockPlanUsageLimitService = new Mock<IPlanUsageLimitService>();
        _mockSubscriptionAccessService.Setup(x => x.CanUsePersonalGoalsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockPlanUsageLimitService.Setup(x => x.CheckLearningPathCreationAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeNexus.Application.Common.Models.Result.Success());
        
        _handler = new GenerateLearningPathSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockTimelineCalculationService.Object,
            new ThrowingAIGeneratorService(),
            _mockSubscriptionAccessService.Object,
            _mockPlanUsageLimitService.Object
        );
    }

    [Fact]
    public async Task Handle_WithInvalidSubjectId_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest> { new(goalId, 1m) };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Beginner, LanguageSelection.VietNamese);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithInvalidGoalId_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest> { new(goalId, 1m) };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Intermediate, LanguageSelection.English);

        var subject = new Subject { SubjectId = subjectId, Name = "C#" };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new List<CodeNexus.Domain.Entities.Goals>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithTimelineCalculationFailure_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest> { new(goalId, 1m) };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Advanced, LanguageSelection.VietNamese);

        var subject = new Subject { SubjectId = subjectId, Name = "C#" };
        var goal = new CodeNexus.Domain.Entities.Goals 
        { 
            GoalId = goalId, 
            Title = "Master C#", 
            CreatedByUserId = userId,
            IsSystemDefined = false,
            Duration = GoalDuration.TwoMonths // 60 days
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockTimelineCalculationService.Setup(x => x.CalculateChapterTimelinesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<ComplexityLevel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Timeline calculation error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GENERATION_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithValidInputs_ShouldGenerateTimelineBasedLearningPath()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest> { new(goalId, 1m) };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Intermediate, LanguageSelection.English);

        var subject = new Subject { SubjectId = subjectId, Name = "JavaScript" };
        var goal = new CodeNexus.Domain.Entities.Goals 
        { 
            GoalId = goalId, 
            Title = "Become Full Stack Developer", 
            CreatedByUserId = userId,
            IsSystemDefined = false,
            Duration = GoalDuration.OneMonth // 30 days
        };

        // Mock chapter timelines (30 days = ~4 chapters of 1 week each)
        var chapterTimelines = new List<ChapterTimelineDto>
        {
            new(0, DateTime.UtcNow, DateTime.UtcNow.AddDays(6), 7),
            new(1, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(13), 7),
            new(2, DateTime.UtcNow.AddDays(14), DateTime.UtcNow.AddDays(20), 7),
            new(3, DateTime.UtcNow.AddDays(21), DateTime.UtcNow.AddDays(29), 7)
        };

        // Mock lesson schedules (5 lessons per chapter for intermediate)
        var lessonSchedules = new List<LessonScheduleDto>
        {
            new(0, DateTime.UtcNow), // Use LessonDay only
            new(1, DateTime.UtcNow.AddDays(1)),
            new(2, DateTime.UtcNow.AddDays(2)),
            new(3, DateTime.UtcNow.AddDays(3)),
            new(4, DateTime.UtcNow.AddDays(4))
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        
        _mockTimelineCalculationService.Setup(x => x.CalculateChapterTimelinesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<ComplexityLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapterTimelines);
        _mockTimelineCalculationService.Setup(x => x.CalculateLessonSchedulesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<ComplexityLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lessonSchedules);
        _mockTimelineCalculationService.Setup(x => x.GetQuizzesPerLesson(ComplexityLevel.Intermediate))
            .Returns(1); // 1 quiz per lesson for intermediate

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(4, result.Value.ChapterCount); // 4 chapters for 30 days
        Assert.Equal(4, result.Value.ChapterDtos.Count);
        Assert.Equal("JavaScript: Become Full Stack Developer", result.Value.Title);
        
        // Verify each chapter has the expected number of lessons (5 for intermediate)
        foreach (var chapter in result.Value.ChapterDtos)
        {
            Assert.False(string.IsNullOrWhiteSpace(chapter.Content));
            Assert.Equal(5, chapter.Lessons.Count);

            // Response does not include quizzes yet (quizzes are created in DB)
            foreach (var lesson in chapter.Lessons)
            {
                Assert.NotNull(lesson.Quizzes);
                Assert.Empty(lesson.Quizzes);
            }
        }

        // Verify timeline calculation service was called correctly
        _mockTimelineCalculationService.Verify(x => x.CalculateChapterTimelinesAsync(
            It.IsAny<DateTime>(), 
            It.IsAny<DateTime>(), 
            0, // Not used anymore
            ComplexityLevel.Intermediate, 
            It.IsAny<CancellationToken>()), Times.Once);
        
        _mockTimelineCalculationService.Verify(x => x.CalculateLessonSchedulesAsync(
            It.IsAny<DateTime>(), 
            It.IsAny<DateTime>(), 
            5, // 5 lessons per chapter for intermediate
            ComplexityLevel.Intermediate, 
            It.IsAny<CancellationToken>()), Times.Exactly(4)); // Called for each chapter
        
        _mockTimelineCalculationService.Verify(x => x.GetQuizzesPerLesson(ComplexityLevel.Intermediate), Times.Exactly(20)); // Called for each lesson (4 chapters * 5 lessons)
    }

    [Fact]
    public async Task Handle_WhenSubjectNotFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest> { new(goalId, 1m) };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Beginner, LanguageSelection.VietNamese);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_NOT_FOUND", result.ErrorCode);
    }

    private sealed class ThrowingAIGeneratorService : IAIGeneratorService
    {
        public Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            throw new Exception("AI not available");
        }

        public Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            throw new Exception("AI not available");
        }
    }
}
