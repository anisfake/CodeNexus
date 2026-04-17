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
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly Mock<IPlanUsageLimitService> _mockPlanUsageLimitService;
    private readonly GenerateLearningPathSkeletonCommandHandler _handler;

    public GenerateLearningPathSkeletonCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockTimelineCalculationService = new Mock<ITimelineCalculationService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();
        _mockPlanUsageLimitService = new Mock<IPlanUsageLimitService>();
        _mockPlanUsageLimitService.Setup(x => x.CheckLearningPathCreationAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeNexus.Application.Common.Models.Result.Success());
        _mockContext.Setup(x => x.Users).Returns(new List<User>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new List<AIProviderConfig>().BuildMockDbSet().Object);
        
        _handler = new GenerateLearningPathSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockTimelineCalculationService.Object,
            new MockAIGeneratorService(),
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
        if (!result.IsSuccess)
        {
            Assert.Fail($"Expected success but got error: {result.ErrorCode} - {result.ErrorMessage}");
        }
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
    public async Task Handle_WithValidInputs_ShouldGenerateTimelineBasedLearningPath_Debug()
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

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Mock timeline calculation to return empty lists to avoid AI calls
        _mockTimelineCalculationService.Setup(x => x.CalculateChapterTimelinesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<ComplexityLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChapterTimelineDto>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Should succeed even with empty chapters
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.ChapterCount);
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

    [Fact]
    public async Task Handle_WhenPaidBalanceNotEnoughForFullGeneration_ShouldRejectImmediately()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest> { new(goalId, 1m) };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Intermediate, LanguageSelection.English);

        var subject = new Subject { SubjectId = subjectId, Name = "Python" };
        var goal = new CodeNexus.Domain.Entities.Goals
        {
            GoalId = goalId,
            Title = "Build ML Pipeline",
            CreatedByUserId = userId,
            IsSystemDefined = false,
            Duration = GoalDuration.OneMonth
        };

        var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var user = new User
        {
            UserId = userId,
            RoleId = role.RoleId,
            Role = role,
            TokenBalance = 10m
        };

        var paidConfig = new AIProviderConfig
        {
            ConfigId = Guid.NewGuid(),
            AccessTier = AIAccessTier.Paid,
            UsageType = AIUsageType.StructureGeneration,
            IsActive = true,
            ConfigJson = "{\"MaxTokens\":8192,\"InputCostPer1M\":200,\"OutputCostPer1M\":600}"
        };

        var chapterTimelines = new List<ChapterTimelineDto>
        {
            new(0, DateTime.UtcNow, DateTime.UtcNow.AddDays(6), 7),
            new(1, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(13), 7),
            new(2, DateTime.UtcNow.AddDays(14), DateTime.UtcNow.AddDays(20), 7),
            new(3, DateTime.UtcNow.AddDays(21), DateTime.UtcNow.AddDays(29), 7)
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new[] { paidConfig }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);

        _mockTimelineCalculationService.Setup(x => x.CalculateChapterTimelinesAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<ComplexityLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapterTimelines);
        _mockTimelineCalculationService.Setup(x => x.GetQuizzesPerLesson(ComplexityLevel.Intermediate))
            .Returns(2);

        var aiSpy = new CountingAIGeneratorService();
        var localHandler = new GenerateLearningPathSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockTimelineCalculationService.Object,
            aiSpy,
            _mockPlanUsageLimitService.Object);

        // Act
        var result = await localHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INSUFFICIENT_TOKEN_BALANCE", result.ErrorCode);
        Assert.Equal(0, aiSpy.StructureCallCount);
    }

    [Fact]
    public async Task Handle_WithTwoGoals_ShouldIncludeGoalPercentagesInAIPrompts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalAId = Guid.NewGuid();
        var goalBId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest>
        {
            new(goalAId, 70m),
            new(goalBId, 30m)
        };
        var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, ComplexityLevel.Beginner, LanguageSelection.VietNamese);

        var subject = new Subject { SubjectId = subjectId, Name = "Python" };
        var goalA = new CodeNexus.Domain.Entities.Goals
        {
            GoalId = goalAId,
            Title = "Build End-to-End ML Pipeline",
            CreatedByUserId = userId,
            IsSystemDefined = false,
            Duration = GoalDuration.TwoMonths
        };
        var goalB = new CodeNexus.Domain.Entities.Goals
        {
            GoalId = goalBId,
            Title = "Implement Code Quality & Reviews",
            CreatedByUserId = userId,
            IsSystemDefined = false,
            Duration = GoalDuration.OneMonth
        };

        var chapterTimelines = new List<ChapterTimelineDto>
        {
            new(0, DateTime.UtcNow, DateTime.UtcNow.AddDays(6), 7)
        };
        var lessonSchedules = new List<LessonScheduleDto>
        {
            new(0, DateTime.UtcNow),
            new(1, DateTime.UtcNow.AddDays(1)),
            new(2, DateTime.UtcNow.AddDays(2)),
            new(3, DateTime.UtcNow.AddDays(3))
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goalA, goalB }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mockTimelineCalculationService.Setup(x => x.CalculateChapterTimelinesAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<ComplexityLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapterTimelines);
        _mockTimelineCalculationService.Setup(x => x.CalculateLessonSchedulesAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<ComplexityLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(lessonSchedules);
        _mockTimelineCalculationService.Setup(x => x.GetQuizzesPerLesson(ComplexityLevel.Beginner))
            .Returns(1);

        var aiSpy = new PromptCaptureAIGeneratorService();
        var localHandler = new GenerateLearningPathSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockTimelineCalculationService.Object,
            aiSpy,
            _mockPlanUsageLimitService.Object);

        // Act
        var result = await localHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(aiSpy.Prompts, p => p.Contains("Goal Priorities:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(aiSpy.Prompts, p => p.Contains("(70", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(aiSpy.Prompts, p => p.Contains("(30", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CountingAIGeneratorService : IAIGeneratorService
    {
        public int StructureCallCount { get; private set; }

        public Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            StructureCallCount++;
            throw new InvalidOperationException("AI should not be called for insufficient upfront budget check.");
        }

        public Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
            => Task.FromResult(string.Empty);
    }

    private sealed class PromptCaptureAIGeneratorService : IAIGeneratorService
    {
        public List<string> Prompts { get; } = new();

        public Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            Prompts.Add(prompt);
            throw new InvalidOperationException("Capture prompt only.");
        }

        public Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            Prompts.Add(prompt);
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class MockAIGeneratorService : IAIGeneratorService
    {
        public Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            if (typeof(T) == typeof(ChapterGenerationData))
            {
                var chapterData = new ChapterGenerationData
                {
                    Title = "Sample Chapter",
                    Content = "Sample chapter content",
                    LessonTitles = new List<string> { "Lesson 1", "Lesson 2", "Lesson 3", "Lesson 4", "Lesson 5" }
                };
                return Task.FromResult((T)(object)chapterData);
            }
            
            if (typeof(T) == typeof(LearningPathMeta))
            {
                var pathMeta = new LearningPathMeta
                {
                    Title = "JavaScript: Become Full Stack Developer",
                    Description = "Complete learning path for JavaScript development"
                };
                return Task.FromResult((T)(object)pathMeta);
            }
            
            if (typeof(T) == typeof(QuizTitleGenerationData))
            {
                var quizData = new QuizTitleGenerationData
                {
                    Titles = new List<string> { "Quiz 1" }
                };
                return Task.FromResult((T)(object)quizData);
            }
            
            throw new NotSupportedException($"Type {typeof(T)} not supported in mock");
        }

        public Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            return Task.FromResult("Generated content");
        }
    }

    private class ChapterGenerationData
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public List<string> LessonTitles { get; set; } = new();
    }

    private class QuizTitleGenerationData
    {
        public List<string> Titles { get; set; } = new();
    }

    private sealed class LearningPathMeta
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
