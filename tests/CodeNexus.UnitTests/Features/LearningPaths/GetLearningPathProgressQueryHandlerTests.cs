using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPaths.Queries.GetLearningPathProgress;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetLearningPathProgressQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetLearningPathProgressQueryHandler _handler;

    public GetLearningPathProgressQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetLearningPathProgressQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }


    [Fact]
    public async Task Handle_ValidData_ReturnsProgressUsingEverPassedAndCompletedTasks()
    {
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = pathId,
            IsDeleted = false,
            Title = "Chapter 1"
        };

        var lesson = new Lesson
        {
            LessonId = NewId.NextGuid(),
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            IsDeleted = false,
            Title = "Lesson 1",
            Content = "content",
            LessonDay = DateTime.UtcNow
        };

        var quiz1 = new Quiz
        {
            QuizId = NewId.NextGuid(),
            LessonId = lesson.LessonId,
            Lesson = lesson,
            Title = "Quiz 1",
            IsDeleted = false
        };

        var quiz2 = new Quiz
        {
            QuizId = NewId.NextGuid(),
            LessonId = lesson.LessonId,
            Lesson = lesson,
            Title = "Quiz 2",
            IsDeleted = false
        };

        var task1 = new TaskEntity
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Status = TaskStatus_.Completed,
            Title = "Task 1"
        };

        var task2 = new TaskEntity
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Status = TaskStatus_.Pending,
            Title = "Task 2"
        };

        var attempts = new List<QuizAttempt>
        {
            new()
            {
                AttemptId = NewId.NextGuid(),
                QuizId = quiz1.QuizId,
                Quiz = quiz1,
                UserId = userId,
                Status = QuizAttemptStatus.Passed
            },
            new()
            {
                AttemptId = NewId.NextGuid(),
                QuizId = quiz1.QuizId,
                Quiz = quiz1,
                UserId = userId,
                Status = QuizAttemptStatus.NotPassed
            },
            new()
            {
                AttemptId = NewId.NextGuid(),
                QuizId = quiz1.QuizId,
                Quiz = quiz1,
                UserId = userId,
                Status = QuizAttemptStatus.Passed
            },
            new()
            {
                AttemptId = NewId.NextGuid(),
                QuizId = quiz2.QuizId,
                Quiz = quiz2,
                UserId = userId,
                Status = QuizAttemptStatus.NotPassed
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>
        {
            new() { PathId = pathId, UserId = userId, SubjectId = NewId.NextGuid(), Title = "Path" }
        }.BuildMockDbSet().Object);
_mockContext.Setup(x => x.Lessons).Returns(new List<Lesson> { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>
        {
            new()
            {
                ProgressId = NewId.NextGuid(),
                LessonId = lesson.LessonId,
                Lesson = lesson,
                UserId = userId,
                IsLessonContentRead = true
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz> { quiz1, quiz2 }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(attempts.BuildMockDbSet().Object);
_mockContext.Setup(x => x.Tasks).Returns(new List<TaskEntity> { task1, task2 }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathProgressQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CompletedLessonContents.Should().Be(1);
        result.Value.TotalLessonContents.Should().Be(1);
        result.Value.ContentProgressPercent.Should().Be(100m);
        result.Value!.CompletedQuizzes.Should().Be(1);
        result.Value.TotalQuizzes.Should().Be(2);
        result.Value.QuizProgressPercent.Should().Be(50m);
        result.Value.CompletedTasks.Should().Be(1);
        result.Value.TotalTasks.Should().Be(2);
        result.Value.ProgressPercent.Should().Be(60m);
        result.Value.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task Handle_PathNotFound_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(new List<QuizAttempt>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Tasks).Returns(new List<TaskEntity>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathProgressQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_PathOwnedByAnotherUser_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();
        var ownerId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>
        {
            new() { PathId = pathId, UserId = ownerId, SubjectId = NewId.NextGuid(), Title = "Path" }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(new List<QuizAttempt>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Tasks).Returns(new List<TaskEntity>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathProgressQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
