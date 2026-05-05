using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestionPreview;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetLearningPathSuggestionPreviewQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IGoalValidationService> _mockGoalValidationService;
    private readonly GetLearningPathSuggestionPreviewQueryHandler _handler;

    public GetLearningPathSuggestionPreviewQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockGoalValidationService = new Mock<IGoalValidationService>();

        _handler = new GetLearningPathSuggestionPreviewQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockGoalValidationService.Object);
    }

    [Fact]
    public async Task Handle_WithValidSuggestedPath_ShouldReturnPreview()
    {
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var quizId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Python",
            Description = "Python subject"
        };

        var goal = new CodeNexus.Domain.Entities.Goals
        {
            GoalId = goalId,
            Title = "Build API",
            Description = "Build backend API",
            IsSystemDefined = true,
            Duration = GoalDuration.OneMonth
        };

        var lesson = new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapterId,
            Title = "Lesson 1",
            Content = "Lesson content",
            OrderIndex = 0,
            LessonDay = DateTime.UtcNow.Date,
            IsDeleted = false,
            Quizzes = new List<Quiz>
            {
                new()
                {
                    QuizId = quizId,
                    LessonId = lessonId,
                    Title = "Quiz 1",
                    Description = "Quiz description",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }
            }
        };

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Chapter 1",
            Content = "Chapter content",
            OrderIndex = 0,
            IsDeleted = false,
            Lessons = new List<Lesson> { lesson },
            Tasks = new List<CodeNexus.Domain.Entities.Tasks>
            {
                new()
                {
                    TaskId = taskId,
                    ChapterId = chapterId,
                    PathId = pathId,
                    Title = "Task 1",
                    Description = "Task description",
                    TaskType = TaskType.Practice,
                    Status = TaskStatus_.Pending,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }
            }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            SubjectId = subjectId,
            Subject = subject,
            UserId = ownerId,
            User = new User { UserId = ownerId, Username = "mentor-a" },
            Title = "Suggested Path",
            Description = "Path description",
            Language = LanguageSelection.English,
            ComplexityLevel = ComplexityLevel.Beginner,
            Status = LearningPathStatus.Active.ToString(),
            LearningPathGoals = new List<LearningPathGoal>
            {
                new()
                {
                    PathId = pathId,
                    GoalId = goalId,
                    Goal = goal,
                    Weight = 1m
                }
            },
            Chapters = new List<Chapter> { chapter }
        };

        _mockContext.Setup(x => x.Subjects).Returns(new[] { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new[] { new SubjectGoal { SubjectId = subjectId, GoalId = goalId } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.GoalMappings).Returns(new List<GoalMapping>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new[] { new LearningPathGoal { PathId = pathId, GoalId = goalId, Weight = 1m } }.BuildMockDbSet().Object);

        var query = new GetLearningPathSuggestionPreviewQuery(
            pathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.Score.Should().Be(1m);
        result.Value.LearningPath.ChapterDtos.Should().HaveCount(1);
        result.Value.LearningPath.ChapterDtos[0].Lessons.Should().HaveCount(1);
        result.Value.LearningPath.ChapterDtos[0].Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenSuggestedPathBelongsToCurrentUser_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Subjects).Returns(new[]
        {
            new Subject { SubjectId = subjectId, Name = "Python" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals { GoalId = goalId, IsSystemDefined = true, Title = "Build API", Duration = GoalDuration.OneMonth }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubjectGoals).Returns(new[]
        {
            new SubjectGoal { SubjectId = subjectId, GoalId = goalId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.GoalMappings).Returns(new List<GoalMapping>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = pathId,
                SubjectId = subjectId,
                UserId = userId,
                Language = LanguageSelection.English,
                ComplexityLevel = ComplexityLevel.Beginner
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);

        var query = new GetLearningPathSuggestionPreviewQuery(
            pathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CANNOT_PREVIEW_OWN_PATH");
    }

    [Fact]
    public async Task Handle_WhenScoreBelowThreshold_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Subjects).Returns(new[]
        {
            new Subject { SubjectId = subjectId, Name = "C#", Description = "C# subject" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals { GoalId = goalId, Title = "Backend", IsSystemDefined = true, Duration = GoalDuration.OneMonth }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubjectGoals).Returns(new[]
        {
            new SubjectGoal { SubjectId = subjectId, GoalId = goalId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.GoalMappings).Returns(new List<GoalMapping>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = pathId,
                SubjectId = subjectId,
                UserId = ownerId,
                Language = LanguageSelection.English,
                ComplexityLevel = ComplexityLevel.Beginner
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPathGoals).Returns(new[]
        {
            new LearningPathGoal { PathId = pathId, GoalId = goalId, Weight = 0.3m }
        }.BuildMockDbSet().Object);

        var query = new GetLearningPathSuggestionPreviewQuery(
            pathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SUGGESTION_NOT_ELIGIBLE");
    }
}
