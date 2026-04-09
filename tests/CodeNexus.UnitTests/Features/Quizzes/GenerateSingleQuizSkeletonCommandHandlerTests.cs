using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes;

public class GenerateSingleQuizSkeletonCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateSingleQuizSkeletonCommandHandler _handler;

    public GenerateSingleQuizSkeletonCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateSingleQuizSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object);
    }

    [Fact]
    public async Task Handle_LessonNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateSingleQuizSkeletonCommand(NewId.NextGuid());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);

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
        var ownerId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();
        var command = new GenerateSingleQuizSkeletonCommand(lessonId);

        var lesson = CreateLessonGraph(lessonId, ownerId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_LessonTitleMissing_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();
        var command = new GenerateSingleQuizSkeletonCommand(lessonId);

        var lesson = CreateLessonGraph(lessonId, userId);
        lesson.Title = "   ";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LESSON_TITLE_REQUIRED");
        _mockAIGeneratorService.Verify(
            x => x.GenerateStructureAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<CodeNexus.Domain.Enums.AIUsageType>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_LessonContentMissing_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();
        var command = new GenerateSingleQuizSkeletonCommand(lessonId);

        var lesson = CreateLessonGraph(lessonId, userId);
        lesson.Content = "  ";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Lessons).Returns(new[] { lesson }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LESSON_CONTENT_REQUIRED");
        _mockAIGeneratorService.Verify(
            x => x.GenerateStructureAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<CodeNexus.Domain.Enums.AIUsageType>()),
            Times.Never);
    }

    private static Lesson CreateLessonGraph(Guid lessonId, Guid ownerId)
    {
        var subject = new Subject
        {
            SubjectId = NewId.NextGuid(),
            Name = "C#",
            CreatedByUserId = ownerId
        };

        var learningPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = ownerId,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "C# Learning Path",
            Description = "Learn C# from basics"
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = learningPath.PathId,
            LearningPath = learningPath,
            Title = "Basics",
            Content = "C# Basics"
        };

        return new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Title = "Variables",
            Content = "Variable types"
        };
    }
}
