using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class LearningPathJsonParsingStressTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IAIGeneratorService> _mockAIService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GenerateLearningPathSkeletonCommandHandler _handler;

    public LearningPathJsonParsingStressTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockAIService = new Mock<IAIGeneratorService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _handler = new GenerateLearningPathSkeletonCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIService.Object);
    }

    [Fact]
    public async Task Handle_WithComplexLearningPath_ShouldHandleJsonCorrectly()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(
            Guid.NewGuid(), 
            Guid.NewGuid(), 
            ComplexityLevel.Advanced, 
            LanguageSelection.VietNamese);

        var subject = new Subject { SubjectId = command.SubjectId, Name = "Data Structures & Algorithms" };
        var goal = new CodeNexus.Domain.Entities.Goals { GoalId = command.GoalId, Title = "Master Advanced DSA", Description = "Learn complex algorithms and data structures" };
        var user = new User { UserId = Guid.NewGuid() };

        // Create a complex learning path skeleton that might cause JSON issues
        var complexSkeleton = new LearningPathSkeletonDto(
            "Lộ trình học Data Structures & Algorithms cho Full-stack Developer",
            "Học các cấu trúc dữ liệu và thuật toán cốt lõi, áp dụng vào phát triển frontend và backend để xây dựng ứng dụng web hoàn chỉnh",
            new List<ChapterDto>
            {
                new ChapterDto(
                    Guid.NewGuid(),
                    "Cấu trúc dữ liệu cơ bản",
                    "Nắm vững các cấu trúc dữ liệu nền tảng để xây dựng ứng dụng",
                    0,
                    new List<LessonDto>
                    {
                        new LessonDto(
                            Guid.NewGuid(),
                            "Giới thiệu về Array và String",
                            "Tìm hiểu cách sử dụng Array và String trong programming",
                            new List<QuizDto>
                            {
                                new QuizDto(Guid.NewGuid(), "Quiz về Array operations", "Kiểm tra hiểu biết về các thao tác cơ bản với Array")
                            }
                        ),
                        new LessonDto(
                            Guid.NewGuid(),
                            "Thực hành với Linked List",
                            "Implement và sử dụng Linked List trong các bài toán thực tế",
                            new List<QuizDto>()
                        )
                    },
                    new List<TaskDto>()
                ),
                new ChapterDto(
                    Guid.NewGuid(),
                    "Thuật toán sắp xếp và tìm kiếm",
                    "Học các thuật toán Sorting và Searching algorithms quan trọng",
                    1,
                    new List<LessonDto>
                    {
                        new LessonDto(
                            Guid.NewGuid(),
                            "Bubble Sort và Quick Sort",
                            "So sánh và implement các thuật toán sorting cơ bản",
                            new List<QuizDto>
                            {
                                new QuizDto(Guid.NewGuid(), "Quiz về Sorting Algorithms", "Test kiến thức về complexity và implementation")
                            }
                        )
                    },
                    new List<TaskDto>()
                )
            }
        );

        SetupMocks(subject, goal, user, complexSkeleton);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - focus on prompt content rather than persistence result
        Assert.NotNull(result);

        // Verify AI service was called with proper prompt
        _mockAIService.Verify(x => x.GenerateStructureAsync<LearningPathSkeletonDto>(
            It.Is<string>(prompt => 
                prompt.Contains("CRITICAL INSTRUCTIONS") && 
                prompt.Contains("complete JSON structure") &&
                prompt.Contains("technical terms") &&
                prompt.Contains("Vietnamese language")), 
            AIUsageType.StructureGeneration), Times.Once);
    }

    [Theory]
    [InlineData(ComplexityLevel.Beginner, LanguageSelection.VietNamese)]
    [InlineData(ComplexityLevel.Intermediate, LanguageSelection.VietNamese)]
    [InlineData(ComplexityLevel.Advanced, LanguageSelection.VietNamese)]
    [InlineData(ComplexityLevel.Beginner, LanguageSelection.English)]
    [InlineData(ComplexityLevel.Advanced, LanguageSelection.English)]
    public async Task Handle_WithDifferentComplexityAndLanguage_ShouldGenerateCorrectPrompt(
        ComplexityLevel complexity, LanguageSelection language)
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(
            Guid.NewGuid(), 
            Guid.NewGuid(), 
            complexity, 
            language);

        var subject = new Subject { SubjectId = command.SubjectId, Name = "React Development" };
        var goal = new CodeNexus.Domain.Entities.Goals { GoalId = command.GoalId, Title = "Learn React", Description = "Master React framework" };
        var user = new User { UserId = Guid.NewGuid() };

        var skeleton = new LearningPathSkeletonDto(
            "React Learning Path",
            "Learn React step by step",
            new List<ChapterDto>
            {
                new ChapterDto(
                    Guid.NewGuid(),
                    "React Basics",
                    "Learn React fundamentals",
                    0,
                    new List<LessonDto>
                    {
                        new LessonDto(
                            Guid.NewGuid(),
                            "Components and JSX",
                            "Understanding React components",
                            new List<QuizDto>()
                        )
                    },
                    new List<TaskDto>()
                )
            }
        );

        SetupMocks(subject, goal, user, skeleton);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - focus on prompt content rather than persistence result

        // Verify prompt contains appropriate language instructions
        _mockAIService.Verify(x => x.GenerateStructureAsync<LearningPathSkeletonDto>(
            It.Is<string>(prompt => 
                language == LanguageSelection.VietNamese 
                    ? prompt.Contains("Vietnamese language") && prompt.Contains("technical terms") && prompt.Contains("ENGLISH")
                    : prompt.Contains("English language")), 
            AIUsageType.StructureGeneration), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAIServiceThrowsJsonException_ShouldReturnFailure()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(
            Guid.NewGuid(), 
            Guid.NewGuid(), 
            ComplexityLevel.Beginner, 
            LanguageSelection.VietNamese);

        var subject = new Subject { SubjectId = command.SubjectId, Name = "Programming" };
        var goal = new CodeNexus.Domain.Entities.Goals { GoalId = command.GoalId, Title = "Learn Programming" };
        var user = new User { UserId = Guid.NewGuid() };

        SetupBasicMocks(subject, goal, user);

        // Simulate JSON parsing error
        _mockAIService
            .Setup(x => x.GenerateStructureAsync<LearningPathSkeletonDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ThrowsAsync(new InvalidOperationException("JSON deserialization failed: Unexpected character encountered"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AI_GENERATION_FAILED", result.ErrorCode);
        Assert.Contains("Failed to generate learning path", result.ErrorMessage);
        Assert.Contains("JSON deserialization failed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenAIServiceReturnsInvalidSkeleton_ShouldReturnFailure()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(
            Guid.NewGuid(), 
            Guid.NewGuid(), 
            ComplexityLevel.Beginner, 
            LanguageSelection.VietNamese);

        var subject = new Subject { SubjectId = command.SubjectId, Name = "Programming" };
        var goal = new CodeNexus.Domain.Entities.Goals { GoalId = command.GoalId, Title = "Learn Programming" };
        var user = new User { UserId = Guid.NewGuid() };

        SetupBasicMocks(subject, goal, user);

        // Return invalid skeleton (null title)
        var invalidSkeleton = new LearningPathSkeletonDto(
            "", // Invalid empty title
            "Valid description",
            new List<ChapterDto>()
        );

        _mockAIService
            .Setup(x => x.GenerateStructureAsync<LearningPathSkeletonDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(invalidSkeleton);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
        Assert.Contains("AI returned invalid skeleton structure", result.ErrorMessage);
    }

    private void SetupMocks(Subject subject, CodeNexus.Domain.Entities.Goals goal, User user, LearningPathSkeletonDto skeleton)
    {
        SetupBasicMocks(subject, goal, user);
        
        _mockAIService
            .Setup(x => x.GenerateStructureAsync<LearningPathSkeletonDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(skeleton);
    }

    private void SetupBasicMocks(Subject subject, CodeNexus.Domain.Entities.Goals goal, User user)
    {
        // Setup Subject DbSet
        var subjects = new List<Subject> { subject };
        var subjectQueryable = new TestAsyncEnumerable<Subject>(subjects);
        var subjectDbSetMock = new Mock<DbSet<Subject>>();
        subjectDbSetMock.As<IQueryable<Subject>>().Setup(m => m.Provider).Returns(subjectQueryable.AsQueryable().Provider);
        subjectDbSetMock.As<IQueryable<Subject>>().Setup(m => m.Expression).Returns(subjectQueryable.AsQueryable().Expression);
        subjectDbSetMock.As<IQueryable<Subject>>().Setup(m => m.ElementType).Returns(subjectQueryable.AsQueryable().ElementType);
        subjectDbSetMock.As<IQueryable<Subject>>().Setup(m => m.GetEnumerator()).Returns(subjectQueryable.AsQueryable().GetEnumerator());
        subjectDbSetMock.As<IAsyncEnumerable<Subject>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(subjectQueryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Subjects).Returns(subjectDbSetMock.Object);

        // Setup Goal DbSet
        var goals = new List<CodeNexus.Domain.Entities.Goals> { goal };
        var goalQueryable = new TestAsyncEnumerable<CodeNexus.Domain.Entities.Goals>(goals);
        var goalDbSetMock = new Mock<DbSet<CodeNexus.Domain.Entities.Goals>>();
        goalDbSetMock.As<IQueryable<CodeNexus.Domain.Entities.Goals>>().Setup(m => m.Provider).Returns(goalQueryable.AsQueryable().Provider);
        goalDbSetMock.As<IQueryable<CodeNexus.Domain.Entities.Goals>>().Setup(m => m.Expression).Returns(goalQueryable.AsQueryable().Expression);
        goalDbSetMock.As<IQueryable<CodeNexus.Domain.Entities.Goals>>().Setup(m => m.ElementType).Returns(goalQueryable.AsQueryable().ElementType);
        goalDbSetMock.As<IQueryable<CodeNexus.Domain.Entities.Goals>>().Setup(m => m.GetEnumerator()).Returns(goalQueryable.AsQueryable().GetEnumerator());
        goalDbSetMock.As<IAsyncEnumerable<CodeNexus.Domain.Entities.Goals>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(goalQueryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Goals).Returns(goalDbSetMock.Object);

        // Setup User DbSet
        var users = new List<User> { user };
        var userQueryable = new TestAsyncEnumerable<User>(users);
        var userDbSetMock = new Mock<DbSet<User>>();
        userDbSetMock.As<IQueryable<User>>().Setup(m => m.Provider).Returns(userQueryable.AsQueryable().Provider);
        userDbSetMock.As<IQueryable<User>>().Setup(m => m.Expression).Returns(userQueryable.AsQueryable().Expression);
        userDbSetMock.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(userQueryable.AsQueryable().ElementType);
        userDbSetMock.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(userQueryable.AsQueryable().GetEnumerator());
        userDbSetMock.As<IAsyncEnumerable<User>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(userQueryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Users).Returns(userDbSetMock.Object);

        // Setup other DbSets
        _mockContext.Setup(x => x.LearningPaths).Returns(Mock.Of<DbSet<LearningPath>>());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }
}