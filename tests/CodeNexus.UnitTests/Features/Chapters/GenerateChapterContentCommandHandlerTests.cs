using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Chapters;

public class GenerateChapterContentCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateChapterContentCommandHandler _handler;

    public GenerateChapterContentCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateChapterContentCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object
        );
    }

    [Fact]
    public async Task Handle_ChapterNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var command = new GenerateChapterContentCommand(chapterId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new List<Chapter>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CHAPTER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var command = new GenerateChapterContentCommand(NewId.NextGuid());

        var (chapter, _) = CreateChapterGraph(command.ChapterId, otherUserId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_ContentAlreadyGenerated_ReturnsExistingContent()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterContentCommand(NewId.NextGuid());

        var (chapter, _) = CreateChapterGraph(command.ChapterId, userId);
        chapter.Content = "Full generated chapter description...";
        chapter.UpdatedAt = DateTime.Now;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ChapterId.Should().Be(chapter.ChapterId);
        result.Value.Content.Should().Be("Full generated chapter description...");
        _mockAIGeneratorService.Verify(
            x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoContentYet_GeneratesSavesAndReturns()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterContentCommand(NewId.NextGuid());

        var (chapter, _) = CreateChapterGraph(command.ChapterId, userId);
        chapter.Content = "Brief skeleton description";
        chapter.UpdatedAt = null;

        var generatedContent = "## Chapter Overview\nThis chapter covers variables and data types...";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generatedContent);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be(generatedContent);
        chapter.Content.Should().Be(generatedContent);
        chapter.UpdatedAt.Should().NotBeNull();
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AIGenerationFails_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterContentCommand(NewId.NextGuid());

        var (chapter, _) = CreateChapterGraph(command.ChapterId, userId);
        chapter.Content = "Brief description";
        chapter.UpdatedAt = null;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ThrowsAsync(new InvalidOperationException("Groq API timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CONTENT_GENERATION_FAILED");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdatedAtNull_ContentEmpty_StillGenerates()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterContentCommand(NewId.NextGuid());

        var (chapter, _) = CreateChapterGraph(command.ChapterId, userId);
        chapter.Content = string.Empty;
        chapter.UpdatedAt = null;

        var generatedContent = "## Chapter Overview\nNew content here...";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generatedContent);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be(generatedContent);
        _mockAIGeneratorService.Verify(
            x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (Chapter chapter, LearningPath learningPath) CreateChapterGraph(Guid chapterId, Guid userId)
    {
        var subject = new Subject
        {
            SubjectId = NewId.NextGuid(),
            Name = "Python",
            CreatedByUserId = userId
        };

        var learningPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = userId,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Learn Python",
            Description = "Master Python from scratch",
            Chapters = new List<Chapter>()
        };

        var lesson1 = new Lesson
        {
            LessonId = NewId.NextGuid(),
            Title = "Introduction to Variables",
            Content = "Brief description of variables",
            OrderIndex = 0,
            UpdatedAt = null
        };

        var lesson2 = new Lesson
        {
            LessonId = NewId.NextGuid(),
            Title = "Data Types in Python",
            Content = "Brief description of data types",
            OrderIndex = 1,
            UpdatedAt = null
        };

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = learningPath.PathId,
            LearningPath = learningPath,
            Title = "Variables and Data Types",
            Content = "Learn about Python variables",
            OrderIndex = 0,
            Lessons = new List<Lesson> { lesson1, lesson2 }
        };

        lesson1.ChapterId = chapter.ChapterId;
        lesson1.Chapter = chapter;
        lesson2.ChapterId = chapter.ChapterId;
        lesson2.Chapter = chapter;

        learningPath.Chapters = new List<Chapter> { chapter };

        return (chapter, learningPath);
    }
}
