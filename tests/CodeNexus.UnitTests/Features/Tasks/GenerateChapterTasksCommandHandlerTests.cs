using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;
using CodeNexus.Application.Features.Tasks.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Tasks;

public class GenerateChapterTasksCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateChapterTasksCommandHandler _handler;

    public GenerateChapterTasksCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateChapterTasksCommandHandler(
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
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

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
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, otherUserId);

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
    public async Task Handle_ChapterNoLessons_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, userId);
        chapter.Lessons = new List<Lesson>();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CHAPTER_NO_LESSONS");
    }

    [Fact]
    public async Task Handle_TasksAlreadyExist_ReturnsExisting()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, userId);
        chapter.Tasks = new List<Domain.Entities.Tasks>
        {
            new()
            {
                TaskId = NewId.NextGuid(),
                ChapterId = chapter.ChapterId,
                PathId = chapter.PathId,
                Title = "Viết hàm tính giai thừa",
                Description = "Viết một hàm nhận vào số nguyên n và trả về n!.",
                Priority = TaskPriority.Medium,
                Status = TaskStatus_.Pending
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ChapterId.Should().Be(chapter.ChapterId);
        result.Value.Tasks.Should().HaveCount(1);
        result.Value.Tasks[0].Title.Should().Be("Viết hàm tính giai thừa");
        _mockAIGeneratorService.Verify(
            x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoTasksYet_GeneratesSavesAndReturns()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, userId);

        var generated = new GeneratedTasksDto(new List<GeneratedTaskItemDto>
        {
            new("Viết hàm tính giai thừa", "Viết một hàm nhận vào số nguyên n và trả về n!.", "Medium"),
            new("Kiểm tra số nguyên tố", "Viết chương trình kiểm tra một số có phải số nguyên tố không.", "High")
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>()))
            .ReturnsAsync(generated);

        var tasksList = new List<Domain.Entities.Tasks>();
        _mockContext.Setup(x => x.Tasks).Returns(
            tasksList.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ChapterId.Should().Be(chapter.ChapterId);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AIReturnsEmptyTasks_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, userId);

        var generated = new GeneratedTasksDto(new List<GeneratedTaskItemDto>());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>()))
            .ReturnsAsync(generated);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_AI_RESPONSE");
    }

    [Fact]
    public async Task Handle_AIReturnsNull_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, userId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>()))
            .ReturnsAsync((GeneratedTasksDto)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_AI_RESPONSE");
    }

    [Fact]
    public async Task Handle_AIGenerationFails_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        var chapter = CreateChapterGraph(command.ChapterId, userId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(
            new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Groq API timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TASK_GENERATION_FAILED");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Chapter CreateChapterGraph(Guid chapterId, Guid userId)
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

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = learningPath.PathId,
            LearningPath = learningPath,
            Title = "Variables and Data Types",
            Content = "Learn about Python variables",
            OrderIndex = 0,
            Lessons = new List<Lesson>
            {
                new()
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapterId,
                    Title = "Introduction to Variables",
                    Content = "Variables in Python are containers...",
                    OrderIndex = 0
                },
                new()
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapterId,
                    Title = "Data Types Overview",
                    Content = "Python has several built-in data types...",
                    OrderIndex = 1
                }
            },
            Tasks = new List<Domain.Entities.Tasks>()
        };

        learningPath.Chapters = new List<Chapter> { chapter };

        return chapter;
    }
}
