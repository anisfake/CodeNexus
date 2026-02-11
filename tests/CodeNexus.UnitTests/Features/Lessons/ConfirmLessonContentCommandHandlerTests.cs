using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Lessons.Commands.ConfirmLessonContent;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Lessons;

public class ConfirmLessonContentCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly ConfirmLessonContentCommandHandler _handler;

    public ConfirmLessonContentCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        _handler = new ConfirmLessonContentCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object
        );
    }

    [Fact]
    public async Task Handle_LessonNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new ConfirmLessonContentCommand(NewId.NextGuid(), "Some content");

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(
            new List<Lesson>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var lesson = CreateLesson(NewId.NextGuid(), otherUserId);
        var command = new ConfirmLessonContentCommand(lesson.LessonId, "Some content");

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(
            new[] { lesson }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_ValidRequest_SavesContentAndReturnsSuccess()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var lesson = CreateLesson(NewId.NextGuid(), userId);
        var confirmedContent = "## Overview\nFull lesson content here...";
        var command = new ConfirmLessonContentCommand(lesson.LessonId, confirmedContent);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(
            new[] { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lesson.Content.Should().Be(confirmedContent);
        lesson.UpdatedAt.Should().NotBeNull();
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Lesson CreateLesson(Guid lessonId, Guid userId)
    {
        var learningPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = userId,
            SubjectId = NewId.NextGuid(),
            Title = "Learn Python"
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = learningPath.PathId,
            LearningPath = learningPath,
            Title = "Chapter 1",
            OrderIndex = 0
        };

        return new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Title = "Lesson 1",
            Content = "Brief description",
            OrderIndex = 0,
            UpdatedAt = null
        };
    }
}
