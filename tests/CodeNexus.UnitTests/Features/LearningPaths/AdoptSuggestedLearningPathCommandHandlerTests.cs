using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class AdoptSuggestedLearningPathCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ITimelineCalculationService> _mockTimelineCalculationService;
    private readonly Mock<ISubscriptionAccessService> _mockSubscriptionAccessService;
    private readonly Mock<IPlanUsageLimitService> _mockPlanUsageLimitService;
    private readonly AdoptSuggestedLearningPathCommandHandler _handler;

    public AdoptSuggestedLearningPathCommandHandlerTests()
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

        _handler = new AdoptSuggestedLearningPathCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockTimelineCalculationService.Object,
            _mockSubscriptionAccessService.Object,
            _mockPlanUsageLimitService.Object);
    }

    [Fact]
    public async Task Handle_WhenSubjectMissing_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var suggestedPathId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new AdoptSuggestedLearningPathCommand(
            suggestedPathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithMissingSubject_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var suggestedPathId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new AdoptSuggestedLearningPathCommand(
            suggestedPathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithSuggestionContextMismatch_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        var suggestedPathId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new AdoptSuggestedLearningPathCommand(
            suggestedPathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Python" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals
            {
                GoalId = goalId,
                Title = "Build API",
                IsSystemDefined = false,
                CreatedByUserId = userId,
                Duration = GoalDuration.OneMonth
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = suggestedPathId,
                SubjectId = otherSubjectId,
                ComplexityLevel = ComplexityLevel.Beginner,
                Language = LanguageSelection.English,
                Title = "Suggested",
                UserId = Guid.NewGuid()
            }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUGGESTION_CONTEXT_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenSuggestedPathBelongsToCurrentUser_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var suggestedPathId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var command = new AdoptSuggestedLearningPathCommand(
            suggestedPathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Python" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals
            {
                GoalId = goalId,
                Title = "Build API",
                IsSystemDefined = false,
                CreatedByUserId = userId,
                Duration = GoalDuration.OneMonth
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = suggestedPathId,
                SubjectId = subjectId,
                ComplexityLevel = ComplexityLevel.Beginner,
                Language = LanguageSelection.English,
                Title = "My own path",
                UserId = userId
            }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CANNOT_ADOPT_OWN_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithValidInputs_ShouldCloneSuggestedPathAndRecalculateSchedule()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var suggestedPathId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var sourceChapterId = Guid.NewGuid();
        var sourceLessonId = Guid.NewGuid();
        var sourceQuizId = Guid.NewGuid();

        var command = new AdoptSuggestedLearningPathCommand(
            suggestedPathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.VietNamese);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Subjects).Returns(new[]
        {
            new Subject { SubjectId = subjectId, Name = "Python" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals
            {
                GoalId = goalId,
                Title = "Build End-to-End ML Pipeline",
                IsSystemDefined = false,
                CreatedByUserId = userId,
                Duration = GoalDuration.OneMonth
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = suggestedPathId,
                SubjectId = subjectId,
                Title = "Lộ trình mẫu",
                Description = "Mô tả mẫu",
                Language = LanguageSelection.VietNamese,
                ComplexityLevel = ComplexityLevel.Beginner,
                UserId = Guid.NewGuid(),
                Status = LearningPathStatus.Active.ToString()
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Chapters).Returns(new[]
        {
            new Chapter
            {
                ChapterId = sourceChapterId,
                PathId = suggestedPathId,
                Title = "Chương 1",
                Content = "Nội dung chương 1",
                OrderIndex = 0,
                IsDeleted = false
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Lessons).Returns(new[]
        {
            new Lesson
            {
                LessonId = sourceLessonId,
                ChapterId = sourceChapterId,
                Title = "Bài 1",
                Content = "Nội dung bài 1",
                OrderIndex = 0,
                IsDeleted = false
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Quizzes).Returns(new[]
        {
            new Quiz
            {
                QuizId = sourceQuizId,
                LessonId = sourceLessonId,
                Title = "Quiz 1",
                Description = "Desc quiz",
                TimeLimit = 10,
                PassingScore = 70,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Questions).Returns(new[]
        {
            new Questions
            {
                QuestionId = Guid.NewGuid(),
                QuizId = sourceQuizId,
                QuestionText = "Question 1",
                Type = QuestionType.MultipleChoice,
                Options = "[\"A\",\"B\"]",
                CorrectAnswer = "A",
                Points = 1,
                OrderIndex = 0
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Tasks).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Tasks
            {
                TaskId = Guid.NewGuid(),
                PathId = suggestedPathId,
                ChapterId = sourceChapterId,
                Title = "Task 1",
                Description = "Desc task",
                Status = TaskStatus_.Completed,
                TaskType = TaskType.Practice,
                Priority = TaskPriority.High,
                CreatedAt = DateTime.UtcNow
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);

        _mockTimelineCalculationService
            .Setup(x => x.CalculateChapterTimelinesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), ComplexityLevel.Beginner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChapterTimelineDto>
            {
                new(0, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(6), 7)
            });

        _mockTimelineCalculationService
            .Setup(x => x.CalculateLessonSchedulesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), ComplexityLevel.Beginner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LessonScheduleDto>
            {
                new(0, DateTime.UtcNow.Date)
            });

        _mockTimelineCalculationService
            .Setup(x => x.CalculateTaskSchedulesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), ComplexityLevel.Beginner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskScheduleDto>
            {
                new(0, DateTime.UtcNow.Date.AddDays(5))
            });

        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Lộ trình mẫu", result.Value.Title);
        Assert.Single(result.Value.ChapterDtos);
        Assert.Single(result.Value.ChapterDtos[0].Lessons);
        Assert.Single(result.Value.ChapterDtos[0].Tasks);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockTimelineCalculationService.Verify(x => x.CalculateChapterTimelinesAsync(
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            1,
            ComplexityLevel.Beginner,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
