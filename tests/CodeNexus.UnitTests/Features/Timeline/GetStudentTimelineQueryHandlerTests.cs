using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Timeline.Queries.GetStudentTimeline;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using DomainTask = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.Timeline;

public class GetStudentTimelineQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetStudentTimelineQueryHandler _handler;

    public GetStudentTimelineQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetStudentTimelineQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldReturnTimelineItems()
    {
        var userId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var quizId = Guid.NewGuid();
        var fromUtc = DateTime.UtcNow.Date.AddDays(-1);
        var toUtc = DateTime.UtcNow.Date.AddDays(2);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var path = new LearningPath
        {
            PathId = pathId,
            UserId = userId,
            Title = "Python Path",
            Status = LearningPathStatus.Active.ToString()
        };

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Chapter 1",
            LearningPath = path
        };

        var lesson = new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapterId,
            Chapter = chapter,
            Title = "Lesson 1",
            LessonDay = DateTime.UtcNow.Date.AddHours(1),
            IsDeleted = false
        };

        var task = new DomainTask
        {
            TaskId = Guid.NewGuid(),
            PathId = pathId,
            ChapterId = chapterId,
            Chapter = chapter,
            Title = "Task 1",
            DueDate = DateTime.UtcNow.Date.AddHours(2),
            Status = TaskStatus_.Pending
        };

        var quiz = new Quiz
        {
            QuizId = quizId,
            LessonId = lessonId,
            Lesson = lesson,
            Title = "Quiz 1",
            DueDate = DateTime.UtcNow.Date.AddHours(3),
            IsDeleted = false
        };

        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { path }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson> { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Tasks).Returns(new List<DomainTask> { task }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz> { quiz }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(new List<QuizAttempt>().BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new GetStudentTimelineQuery(fromUtc, toUtc, pathId, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(3);
        result.Value.Items.Should().Contain(x => x.ItemType == "Lesson");
        result.Value.Items.Should().Contain(x => x.ItemType == "Task");
        result.Value.Items.Should().Contain(x => x.ItemType == "Quiz");
    }

    [Fact]
    public async Task Handle_WithUnknownPath_ShouldReturnNotFoundFailure()
    {
        var userId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Tasks).Returns(new List<DomainTask>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(new List<QuizAttempt>().BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new GetStudentTimelineQuery(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1), pathId, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }
}
