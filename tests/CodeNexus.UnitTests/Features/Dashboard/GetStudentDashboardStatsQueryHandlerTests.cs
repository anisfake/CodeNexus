using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Dashboard.Queries.GetStudentDashboardStats;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.Dashboard;

public class GetStudentDashboardStatsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetStudentDashboardStatsQueryHandler _handler;

    public GetStudentDashboardStatsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetStudentDashboardStatsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new List<User>().AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(new GetStudentDashboardStatsQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UserWithNoData_ReturnsZeroStats()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var user = new User { UserId = userId, Email = "test@example.com", Username = "testuser" };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(new List<QuizAttempt>().AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.FocusSessions).Returns(new List<FocusSession>().AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DailyCheckins).Returns(new List<DailyCheckins>().AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(new GetStudentDashboardStatsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalLessons.Should().Be(0);
        result.Value.CompletedLessons.Should().Be(0);
        result.Value.TotalChapters.Should().Be(0);
        result.Value.CompletedChapters.Should().Be(0);
        result.Value.TotalLearningPaths.Should().Be(0);
        result.Value.TotalQuizAttempts.Should().Be(0);
        result.Value.TotalStudyMinutes.Should().Be(0);
        result.Value.CurrentStreak.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UserWithData_ReturnsCorrectStats()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var otherPathId = NewId.NextGuid();

        var user = new User { UserId = userId, Email = "test@example.com", Username = "testuser" };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = userId,
            Title = "My Path",
            SubjectId = NewId.NextGuid()
        };

        var otherLearningPath = new LearningPath
        {
            PathId = otherPathId,
            UserId = otherUserId,
            Title = "Other Path",
            SubjectId = NewId.NextGuid()
        };

        var completedChapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = pathId,
            LearningPath = learningPath,
            Title = "Chapter 1",
            IsCompleted = true,
            Lessons = new List<Lesson>
            {
                new Lesson { LessonId = NewId.NextGuid(), Title = "Lesson 1" },
                new Lesson { LessonId = NewId.NextGuid(), Title = "Lesson 2" }
            }
        };

        var incompleteChapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = pathId,
            LearningPath = learningPath,
            Title = "Chapter 2",
            IsCompleted = false,
            Lessons = new List<Lesson>
            {
                new Lesson { LessonId = NewId.NextGuid(), Title = "Lesson 3" }
            }
        };

        var otherUserChapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = otherPathId,
            LearningPath = otherLearningPath,
            Title = "Other Chapter",
            IsCompleted = true,
            Lessons = new List<Lesson>
            {
                new Lesson { LessonId = NewId.NextGuid(), Title = "Other Lesson" }
            }
        };

        var quizAttempt = new QuizAttempt
        {
            AttemptId = NewId.NextGuid(),
            UserId = userId,
            QuizId = NewId.NextGuid()
        };

        var userTaskId = NewId.NextGuid();
        var otherTaskId = NewId.NextGuid();

        var userTask = new TaskEntity
        {
            TaskId = userTaskId,
            ChapterId = completedChapter.ChapterId,
            PathId = pathId,
            LearningPath = learningPath,
            Title = "Task 1"
        };

        var otherTask = new TaskEntity
        {
            TaskId = otherTaskId,
            ChapterId = otherUserChapter.ChapterId,
            PathId = otherPathId,
            LearningPath = otherLearningPath,
            Title = "Other Task"
        };

        var focusSessions = new List<FocusSession>
        {
            new FocusSession { SessionId = NewId.NextGuid(), TaskId = userTaskId, Task = userTask, Duration = 30 },
            new FocusSession { SessionId = NewId.NextGuid(), TaskId = userTaskId, Task = userTask, Duration = 45 },
            new FocusSession { SessionId = NewId.NextGuid(), TaskId = otherTaskId, Task = otherTask, Duration = 60 }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { completedChapter, incompleteChapter, otherUserChapter }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(
            new[] { learningPath, otherLearningPath }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { quizAttempt }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.FocusSessions).Returns(
            focusSessions.AsQueryable().BuildMockDbSet().Object);

        var today = DateTime.Today;
        var dailyCheckins = new List<DailyCheckins>
        {
            new DailyCheckins { CheckinId = NewId.NextGuid(), SessionId = focusSessions[0].SessionId, FocusSession = focusSessions[0], CheckinDate = today },
            new DailyCheckins { CheckinId = NewId.NextGuid(), SessionId = focusSessions[1].SessionId, FocusSession = focusSessions[1], CheckinDate = today.AddDays(-1) },
            new DailyCheckins { CheckinId = NewId.NextGuid(), SessionId = focusSessions[0].SessionId, FocusSession = focusSessions[0], CheckinDate = today.AddDays(-2) },
            new DailyCheckins { CheckinId = NewId.NextGuid(), SessionId = focusSessions[2].SessionId, FocusSession = focusSessions[2], CheckinDate = today }
        };

        _mockContext.Setup(x => x.DailyCheckins).Returns(
            dailyCheckins.AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(new GetStudentDashboardStatsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalLessons.Should().Be(3);
        result.Value.CompletedLessons.Should().Be(2);
        result.Value.TotalChapters.Should().Be(2);
        result.Value.CompletedChapters.Should().Be(1);
        result.Value.TotalLearningPaths.Should().Be(1);
        result.Value.TotalQuizAttempts.Should().Be(1);
        result.Value.TotalStudyMinutes.Should().Be(75);
        result.Value.CurrentStreak.Should().Be(3);
    }

    [Theory]
    [InlineData(new[] { 0, -1, -2 }, 3)]
    [InlineData(new[] { -1, -2, -3 }, 3)]
    [InlineData(new[] { -2, -3 }, 0)]
    [InlineData(new int[0], 0)]
    [InlineData(new[] { 0 }, 1)]
    [InlineData(new[] { 0, -2 }, 1)]
    [InlineData(new[] { -1 }, 1)]
    public void CalculateStreak_VariousScenarios_ReturnsExpectedStreak(int[] dayOffsets, int expectedStreak)
    {
        // Arrange
        var today = DateTime.Today;
        var dates = dayOffsets.Select(d => today.AddDays(d)).ToList();

        // Act
        var streak = GetStudentDashboardStatsQueryHandler.CalculateStreak(dates, today);

        // Assert
        streak.Should().Be(expectedStreak);
    }
}
