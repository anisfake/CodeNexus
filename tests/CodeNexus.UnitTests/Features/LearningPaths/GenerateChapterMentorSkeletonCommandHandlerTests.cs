using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterMentorSkeleton;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GenerateChapterMentorSkeletonCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;

    public GenerateChapterMentorSkeletonCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
    }

    [Fact]
    public async Task Handle_ChapterTitleEmpty_ReturnsFailure()
    {
        // Arrange
        var aiService = new ReflectionAIGeneratorService(new List<string> { "Lesson 1" });
        var handler = CreateHandler(aiService);
        var command = new GenerateChapterMentorSkeletonCommand(NewId.NextGuid(), "   ", "desc");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CHAPTER_TITLE");
    }

    [Fact]
    public async Task Handle_LearningPathNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var command = new GenerateChapterMentorSkeletonCommand(pathId, "Chapter A", "desc");

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var aiService = new ReflectionAIGeneratorService(new List<string> { "Lesson 1" });
        var handler = CreateHandler(aiService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var ownerId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = ownerId,
            Title = "Backend .NET Path",
            Language = LanguageSelection.English,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Subject = new Subject { SubjectId = NewId.NextGuid(), Name = "ASP.NET Core", CreatedByUserId = ownerId }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);

        var aiService = new ReflectionAIGeneratorService(new List<string> { "Lesson 1" });
        var handler = CreateHandler(aiService);
        var command = new GenerateChapterMentorSkeletonCommand(pathId, "API Security", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_AIReturnsNoLessonTitles_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = userId,
            Title = "Backend .NET Path",
            Language = LanguageSelection.English,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Subject = new Subject { SubjectId = NewId.NextGuid(), Name = "ASP.NET Core", CreatedByUserId = userId }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);

        var aiService = new ReflectionAIGeneratorService(new List<string>());
        var handler = CreateHandler(aiService);
        var command = new GenerateChapterMentorSkeletonCommand(pathId, "API Security", "Protect API endpoints");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_AI_RESPONSE");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessWithComplexityContext()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = userId,
            Title = "Master System Design",
            Language = LanguageSelection.English,
            ComplexityLevel = ComplexityLevel.Advanced,
            Subject = new Subject { SubjectId = NewId.NextGuid(), Name = "System Design", CreatedByUserId = userId }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);

        var aiTitles = new List<string>
        {
            " Intro to Scalable Systems ",
            "Load Balancing Strategies",
            "Caching Patterns",
            "Database Sharding",
            "Fault Tolerance",
            "Observability",
            "Capacity Planning",
            "load balancing strategies"
        };

        var aiService = new ReflectionAIGeneratorService(aiTitles);
        var handler = CreateHandler(aiService);
        var command = new GenerateChapterMentorSkeletonCommand(pathId, "  Building Scalable APIs  ", "  Design for high traffic  ");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.LearningPathTitle.Should().Be("Master System Design");
        result.Value.Language.Should().Be(LanguageSelection.English);
        result.Value.ComplexityLevel.Should().Be(ComplexityLevel.Advanced);
        result.Value.RecommendedChapterCount.Should().Be(7);
        result.Value.ChapterTitle.Should().Be("Building Scalable APIs");
        result.Value.ChapterDescription.Should().Be("Design for high traffic");
        result.Value.Lessons.Should().HaveCount(6);
        result.Value.Lessons.Select(x => x.OrderIndex).Should().ContainInOrder(0, 1, 2, 3, 4, 5);
        result.Value.Lessons.Select(x => x.Title).Should().ContainInOrder(
            "Intro to Scalable Systems",
            "Load Balancing Strategies",
            "Caching Patterns",
            "Database Sharding",
            "Fault Tolerance",
            "Observability");

        aiService.LastPrompt.Should().Contain("Learning Path: Master System Design");
        aiService.LastPrompt.Should().Contain("Language: English");
        aiService.LastPrompt.Should().Contain("Complexity: Advanced");
        aiService.LastPrompt.Should().Contain("Recommended Chapter Count for this complexity: 7");
    }

    [Fact]
    public async Task Handle_AIGenerationThrows_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = userId,
            Title = "Backend Path",
            Language = LanguageSelection.VietNamese,
            ComplexityLevel = ComplexityLevel.Beginner,
            Subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#", CreatedByUserId = userId }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);

        var aiService = ReflectionAIGeneratorService.WithException();
        var handler = CreateHandler(aiService);
        var command = new GenerateChapterMentorSkeletonCommand(pathId, "OOP Basics", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("GENERATION_FAILED");
    }

    private GenerateChapterMentorSkeletonCommandHandler CreateHandler(IAIGeneratorService aiGeneratorService)
    {
        return new GenerateChapterMentorSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            aiGeneratorService);
    }

    private sealed class ReflectionAIGeneratorService : IAIGeneratorService
    {
        private readonly IReadOnlyList<string>? _lessonTitles;
        private readonly Exception? _exception;

        public ReflectionAIGeneratorService(IReadOnlyList<string> lessonTitles)
        {
            _lessonTitles = lessonTitles;
        }

        private ReflectionAIGeneratorService(Exception exception)
        {
            _exception = exception;
        }

        public string LastPrompt { get; private set; } = string.Empty;

        public static ReflectionAIGeneratorService WithException()
        {
            return new ReflectionAIGeneratorService(new InvalidOperationException("AI service failed"));
        }

        public Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            LastPrompt = prompt;

            if (_exception != null)
            {
                throw _exception;
            }

            var result = Activator.CreateInstance(typeof(T))!;
            var lessonTitlesProperty = typeof(T).GetProperty("LessonTitles");
            lessonTitlesProperty?.SetValue(result, _lessonTitles?.ToList() ?? new List<string>());

            return Task.FromResult((T)result);
        }

        public Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
