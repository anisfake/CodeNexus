using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Lessons.Commands.MarkLessonContentRead;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Lessons;

public class MarkLessonContentReadCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly MarkLessonContentReadCommandHandler _handler;

    public MarkLessonContentReadCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockContext.Setup(x => x.DailyCheckins).Returns(new List<DailyCheckins>().BuildMockDbSet().Object);
        _handler = new MarkLessonContentReadCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_LessonNotFound_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new MarkLessonContentReadCommand(lessonId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AccessDenied_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var ownerId = NewId.NextGuid();
        var lesson = CreateLesson(ownerId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new MarkLessonContentReadCommand(lesson.LessonId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_NewProgress_CreatesAndSaves()
    {
        var userId = NewId.NextGuid();
        var lesson = CreateLesson(userId);
        var progresses = new List<LearnProgress>();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(progresses.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new MarkLessonContentReadCommand(lesson.LessonId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mockContext.Verify(x => x.LearnProgresses.Add(It.IsAny<LearnProgress>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingProgress_UpdatesAndSaves()
    {
        var userId = NewId.NextGuid();
        var lesson = CreateLesson(userId);
        var progress = new LearnProgress
        {
            ProgressId = NewId.NextGuid(),
            LessonId = lesson.LessonId,
            Lesson = lesson,
            UserId = userId,
            IsLessonContentRead = false
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new[] { progress }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new MarkLessonContentReadCommand(lesson.LessonId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        progress.IsLessonContentRead.Should().BeTrue();
        progress.UpdatedAt.Should().NotBeNull();
        _mockContext.Verify(x => x.LearnProgresses.Add(It.IsAny<LearnProgress>()), Times.Never);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Lesson CreateLesson(Guid ownerId)
    {
        var path = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = ownerId,
            SubjectId = NewId.NextGuid(),
            Title = "Path"
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = path.PathId,
            LearningPath = path,
            Title = "Chapter"
        };

        return new Lesson
        {
            LessonId = NewId.NextGuid(),
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Title = "Lesson",
            Content = "Content",
            LessonDay = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
