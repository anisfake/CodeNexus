using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Lessons.Queries.GetLessonReadStatus;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Lessons;

public class GetLessonReadStatusQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetLessonReadStatusQueryHandler _handler;

    public GetLessonReadStatusQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetLessonReadStatusQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_LessonNotFound_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLessonReadStatusQuery(lessonId), CancellationToken.None);

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

        var result = await _handler.Handle(new GetLessonReadStatusQuery(lesson.LessonId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_NoProgress_ReturnsUnread()
    {
        var userId = NewId.NextGuid();
        var lesson = CreateLesson(userId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLessonReadStatusQuery(lesson.LessonId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.LessonId.Should().Be(lesson.LessonId);
        result.Value.IsLessonContentRead.Should().BeFalse();
        result.Value.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_HasReadProgress_ReturnsRead()
    {
        var userId = NewId.NextGuid();
        var lesson = CreateLesson(userId);
        var completedAt = DateTime.UtcNow.AddMinutes(-5);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>
        {
            new()
            {
                ProgressId = NewId.NextGuid(),
                LessonId = lesson.LessonId,
                Lesson = lesson,
                UserId = userId,
                IsLessonContentRead = true,
                CompletedAt = completedAt
            }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLessonReadStatusQuery(lesson.LessonId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsLessonContentRead.Should().BeTrue();
        result.Value.ReadAt.Should().Be(completedAt);
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
            Title = "Chapter",
            IsDeleted = false
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
