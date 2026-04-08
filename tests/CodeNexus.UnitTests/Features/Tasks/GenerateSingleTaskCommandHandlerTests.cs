using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;
using CodeNexus.Application.Features.Tasks.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using System.Text.Json;
using Xunit;

namespace CodeNexus.UnitTests.Features.Tasks;

public class GenerateSingleTaskCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateSingleTaskCommandHandler _handler;

    public GenerateSingleTaskCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateSingleTaskCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object
        );
    }

    [Fact]
    public async Task Handle_ChapterWithoutTitle_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var command = new GenerateSingleTaskCommand(chapterId, null, TaskType.Practice);

        var chapter = CreateChapterGraph(chapterId, userId);
        chapter.Title = "   ";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(new[] { chapter }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CHAPTER_TITLE_REQUIRED");
        _mockAIGeneratorService.Verify(
            x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateTaskTitle_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var command = new GenerateSingleTaskCommand(chapterId, null, TaskType.Practice);

        var chapter = CreateChapterGraph(chapterId, userId);
        chapter.Tasks = new List<CodeNexus.Domain.Entities.Tasks>
        {
            new()
            {
                TaskId = NewId.NextGuid(),
                ChapterId = chapterId,
                PathId = chapter.PathId,
                Title = "Implement loop exercises",
                Description = "Existing task",
                TaskType = TaskType.Practice
            }
        };

        var generated = new GeneratedTasksDto(new List<GeneratedTaskItemDto>
        {
            new("Implement loop exercises", "Write for/while loop practice", "High", "Practice", "Check logic", 70, null)
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService
            .Setup(x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generated);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DUPLICATE_TASK");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateQuizQuestionInExistingQuizTask_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var command = new GenerateSingleTaskCommand(chapterId, null, TaskType.Quizz);

        var existingQuizQuestions = new List<QuizQuestionDto>
        {
            new("What is polymorphism?", new List<string> { "A", "B", "C", "D" }, 0)
        };

        var chapter = CreateChapterGraph(chapterId, userId);
        chapter.Tasks = new List<CodeNexus.Domain.Entities.Tasks>
        {
            new()
            {
                TaskId = NewId.NextGuid(),
                ChapterId = chapterId,
                PathId = chapter.PathId,
                Title = "OOP Quiz",
                Description = "Existing quiz task",
                TaskType = TaskType.Quizz,
                QuizQuestionsJson = JsonSerializer.Serialize(existingQuizQuestions)
            }
        };

        var generated = new GeneratedTasksDto(new List<GeneratedTaskItemDto>
        {
            new(
                "New OOP quiz",
                "Quiz about OOP",
                "High",
                "Quizz",
                null,
                80,
                new List<QuizQuestionDto>
                {
                    new("What is polymorphism?", new List<string> { "A", "B", "C", "D" }, 0)
                })
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(new[] { chapter }.BuildMockDbSet().Object);
        _mockAIGeneratorService
            .Setup(x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generated);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DUPLICATE_TASK_QUIZ_QUESTION");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AllLessonTitlesEmpty_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var command = new GenerateSingleTaskCommand(chapterId, null, TaskType.Theory);

        var chapter = CreateChapterGraph(chapterId, userId);
        chapter.Lessons = new List<Lesson>
        {
            new()
            {
                LessonId = NewId.NextGuid(),
                ChapterId = chapterId,
                Title = "",
                Content = "Lesson content 1",
                OrderIndex = 0
            },
            new()
            {
                LessonId = NewId.NextGuid(),
                ChapterId = chapterId,
                Title = "   ",
                Content = "Lesson content 2",
                OrderIndex = 1
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Chapters).Returns(new[] { chapter }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LESSON_TITLE_REQUIRED");
        _mockAIGeneratorService.Verify(
            x => x.GenerateStructureAsync<GeneratedTasksDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()),
            Times.Never);
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
                }
            },
            Tasks = new List<CodeNexus.Domain.Entities.Tasks>()
        };

        learningPath.Chapters = new List<Chapter> { chapter };

        return chapter;
    }
}
