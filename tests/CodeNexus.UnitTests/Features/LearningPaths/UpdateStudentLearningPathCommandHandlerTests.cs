using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateStudentLearningPath;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class UpdateStudentLearningPathCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly UpdateStudentLearningPathCommandHandler _handler;

    public UpdateStudentLearningPathCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new UpdateStudentLearningPathCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object);
    }

    private LearningPath BuildActivePath(Guid pathId, Guid studentId)
    {
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var lesson = new Lesson
        {
            LessonId = NewId.NextGuid(),
            Title = "Lesson 1",
            OrderIndex = 0,
            LessonDay = lessonDay,
            IsDeleted = false,
            Content = string.Empty,
            Quizzes = new List<Quiz>
            {
                new()
                {
                    QuizId = NewId.NextGuid(),
                    Title = "Quiz 1",
                    IsDeleted = false,
                    Questions = new List<Questions>()
                }
            }
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = pathId,
            Title = "Chapter 1",
            OrderIndex = 0,
            IsDeleted = false,
            Lessons = new List<Lesson> { lesson },
            Tasks = new List<Domain.Entities.Tasks>
            {
                new()
                {
                    TaskId = NewId.NextGuid(),
                    Title = "Task 1",
                    IsDeleted = false,
                    Status = TaskStatus_.Pending
                }
            }
        };

        return new LearningPath
        {
            PathId = pathId,
            UserId = studentId,
            SubjectId = NewId.NextGuid(),
            Title = "My Active Path",
            Description = "Student description",
            Status = LearningPathStatus.Active.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.English,
            CreatedAt = DateTime.UtcNow,
            Chapters = new List<Chapter> { chapter },
            LearningPathGoals = new List<LearningPathGoal>(),
            Subject = new Subject { SubjectId = NewId.NextGuid(), Name = "Programming" }
        };
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesChaptersAndPreservesQuizzesAndTasks()
    {
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var activePath = BuildActivePath(pathId, studentId);
        var originalQuizId = activePath.Chapters.First().Lessons.First().Quizzes.First().QuizId;
        var originalTaskId = activePath.Chapters.First().Tasks.First().TaskId;
        var lessonDay = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { activePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateStudentLearningPathCommand(
            pathId,
            new List<StudentChapterRequest>
            {
                new("Updated Chapter 1", null, null, null,
                    new List<StudentLessonRequest>
                    {
                        new("Updated Lesson 1", lessonDay)
                    })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Chapter title should be updated
        activePath.Chapters.First().Title.Should().Be("Updated Chapter 1");
        // Lesson title should be updated
        activePath.Chapters.First().Lessons.First().Title.Should().Be("Updated Lesson 1");
        // Quiz should be preserved
        activePath.Chapters.First().Lessons.First().Quizzes.First().QuizId.Should().Be(originalQuizId);
        // Task should be preserved
        activePath.Chapters.First().Tasks.First().TaskId.Should().Be(originalTaskId);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PathNotFound_ReturnsFailure()
    {
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var command = new UpdateStudentLearningPathCommand(
            pathId,
            new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest> { new("Lesson 1", DateTime.UtcNow) })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_PathBelongsToAnotherUser_ReturnsFailure()
    {
        var studentId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var activePath = BuildActivePath(pathId, otherUserId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { activePath }.BuildMockDbSet().Object);

        var command = new UpdateStudentLearningPathCommand(
            pathId,
            new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest> { new("Lesson 1", DateTime.UtcNow) })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_PathStatusNotActive_ReturnsInvalidStatus()
    {
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var path = BuildActivePath(pathId, studentId);
        path.Status = LearningPathStatus.Draft.ToString();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { path }.BuildMockDbSet().Object);

        var command = new UpdateStudentLearningPathCommand(
            pathId,
            new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest> { new("Lesson 1", DateTime.UtcNow) })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_STATUS");
    }

    [Fact]
    public async Task Handle_AddingNewChapter_CreatesChapterWithNoTasks()
    {
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var activePath = BuildActivePath(pathId, studentId);
        var lessonDay = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

        var addedChapters = new List<Chapter>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { activePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters.AddAsync(It.IsAny<Chapter>(), It.IsAny<CancellationToken>()))
            .Callback<Chapter, CancellationToken>((c, _) => addedChapters.Add(c))
            .ReturnsAsync((Chapter c, CancellationToken _) => null!);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateStudentLearningPathCommand(
            pathId,
            new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest> { new("Lesson 1", lessonDay) }),
                new("New Chapter 2", null, null, null,
                    new List<StudentLessonRequest> { new("Lesson A", lessonDay) })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedChapters.Should().HaveCount(1);
        addedChapters[0].Title.Should().Be("New Chapter 2");
        addedChapters[0].Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RemovingChapter_SoftDeletesChapterAndPreservesOthers()
    {
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var activePath = BuildActivePath(pathId, studentId);
        var extraChapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = pathId,
            Title = "Chapter 2",
            OrderIndex = 1,
            IsDeleted = false,
            Lessons = new List<Lesson>
            {
                new() { LessonId = NewId.NextGuid(), Title = "Lesson X", OrderIndex = 0, IsDeleted = false, LessonDay = DateTime.UtcNow, Quizzes = new List<Quiz>() }
            },
            Tasks = new List<Domain.Entities.Tasks>()
        };
        activePath.Chapters.Add(extraChapter);
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { activePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Only send 1 chapter — chapter at index 1 should be soft-deleted
        var command = new UpdateStudentLearningPathCommand(
            pathId,
            new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest> { new("Lesson 1", lessonDay) })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        extraChapter.IsDeleted.Should().BeTrue();
        extraChapter.DeletedAt.Should().NotBeNull();
    }
}
