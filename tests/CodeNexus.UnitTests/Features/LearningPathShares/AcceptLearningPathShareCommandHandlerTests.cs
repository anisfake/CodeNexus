using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class AcceptLearningPathShareCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly AcceptLearningPathShareCommandHandler _handler;

    public AcceptLearningPathShareCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new AcceptLearningPathShareCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_PendingShare_ClonesPathForStudentAndAcceptsShare()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var shareId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var goalId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();
        var sourceStartDate = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var sourceEndDate = new DateTime(2026, 01, 31, 0, 0, 0, DateTimeKind.Utc);
        var sourceChapterStartDate = new DateTime(2026, 01, 02, 0, 0, 0, DateTimeKind.Utc);
        var sourceChapterEndDate = new DateTime(2026, 01, 08, 0, 0, 0, DateTimeKind.Utc);
        var sourceLessonDay = new DateTime(2026, 01, 03, 0, 0, 0, DateTimeKind.Utc);
        var sourceQuizDueDate = new DateTime(2026, 01, 04, 0, 0, 0, DateTimeKind.Utc);
        var sourceTaskDueDate = new DateTime(2026, 01, 05, 0, 0, 0, DateTimeKind.Utc);

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Mentor Path",
            Description = "Desc",
            StartDate = sourceStartDate,
            EndDate = sourceEndDate,
            Status = LearningPathStatus.Active.ToString(),
            CreatedByType = true,
            Language = LanguageSelection.VietNamese,
            ComplexityLevel = ComplexityLevel.Beginner,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = sourcePathId, GoalId = goalId, Weight = 100, Goal = new GoalEntity { GoalId = goalId, Title = "Goal", Duration = GoalDuration.OneWeek } }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = chapterId,
                    PathId = sourcePathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    StartDate = sourceChapterStartDate,
                    EndDate = sourceChapterEndDate,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = lessonId,
                            ChapterId = chapterId,
                            Title = "Lesson 1",
                            OrderIndex = 0,
                            LessonDay = sourceLessonDay,
                            Quizzes = new List<Quiz>
                            {
                                new() { QuizId = NewId.NextGuid(), LessonId = lessonId, Title = "Quiz 1", Description = "Q", DueDate = sourceQuizDueDate }
                            }
                        }
                    },
                    Tasks = new List<TaskEntity>
                    {
                        new() { TaskId = NewId.NextGuid(), ChapterId = chapterId, PathId = sourcePathId, Title = "Task 1", TaskType = TaskType.Practice, DueDate = sourceTaskDueDate }
                    }
                }
            }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        var usersDb = new List<User> { student }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare> { share }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { sourcePath }.BuildMockDbSet();

        var learningPathGoalsDb = new List<LearningPathGoal>().BuildMockDbSet();
        var chaptersDb = new List<Chapter>().BuildMockDbSet();
        var lessonsDb = new List<Lesson>().BuildMockDbSet();
        var quizzesDb = new List<Quiz>().BuildMockDbSet();
        var tasksDb = new List<TaskEntity>().BuildMockDbSet();

        LearningPath? createdStudentPath = null;
        Chapter? createdChapter = null;
        Lesson? createdLesson = null;
        Quiz? createdQuiz = null;
        TaskEntity? createdTask = null;

        pathsDb.Setup(x => x.AddAsync(It.IsAny<LearningPath>(), It.IsAny<CancellationToken>()))
            .Callback<LearningPath, CancellationToken>((p, _) => createdStudentPath = p)
            .Returns(ValueTask.FromResult((EntityEntry<LearningPath>)null!));

        chaptersDb.Setup(x => x.AddAsync(It.IsAny<Chapter>(), It.IsAny<CancellationToken>()))
            .Callback<Chapter, CancellationToken>((c, _) => createdChapter = c)
            .Returns(ValueTask.FromResult((EntityEntry<Chapter>)null!));

        lessonsDb.Setup(x => x.AddAsync(It.IsAny<Lesson>(), It.IsAny<CancellationToken>()))
            .Callback<Lesson, CancellationToken>((l, _) => createdLesson = l)
            .Returns(ValueTask.FromResult((EntityEntry<Lesson>)null!));

        quizzesDb.Setup(x => x.AddAsync(It.IsAny<Quiz>(), It.IsAny<CancellationToken>()))
            .Callback<Quiz, CancellationToken>((q, _) => createdQuiz = q)
            .Returns(ValueTask.FromResult((EntityEntry<Quiz>)null!));

        tasksDb.Setup(x => x.AddAsync(It.IsAny<TaskEntity>(), It.IsAny<CancellationToken>()))
            .Callback<TaskEntity, CancellationToken>((t, _) => createdTask = t)
            .Returns(ValueTask.FromResult((EntityEntry<TaskEntity>)null!));

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(learningPathGoalsDb.Object);
        _mockContext.Setup(x => x.Chapters).Returns(chaptersDb.Object);
        _mockContext.Setup(x => x.Lessons).Returns(lessonsDb.Object);
        _mockContext.Setup(x => x.Quizzes).Returns(quizzesDb.Object);
        _mockContext.Setup(x => x.Tasks).Returns(tasksDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new AcceptLearningPathShareCommand(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        share.Status.Should().Be(LearningPathShareStatus.Accepted);
        share.RespondedAt.Should().NotBeNull();
        createdStudentPath.Should().NotBeNull();
        createdChapter.Should().NotBeNull();
        createdLesson.Should().NotBeNull();
        createdQuiz.Should().NotBeNull();
        createdTask.Should().NotBeNull();

        var timelineShift = createdStudentPath!.StartDate!.Value - sourceStartDate;
        createdStudentPath.EndDate.Should().Be(sourceEndDate.Add(timelineShift));
        createdChapter!.StartDate.Should().Be(sourceChapterStartDate.Add(timelineShift));
        createdChapter.EndDate.Should().Be(sourceChapterEndDate.Add(timelineShift));
        createdLesson!.LessonDay.Should().Be(sourceLessonDay.Add(timelineShift));
        createdQuiz!.DueDate.Should().Be(sourceQuizDueDate.Add(timelineShift));
        createdTask!.DueDate.Should().Be(sourceTaskDueDate.Add(timelineShift));
        share.RespondedAt.Should().Be(createdStudentPath.StartDate);

        pathsDb.Verify(x => x.AddAsync(
            It.Is<LearningPath>(p => p.UserId == studentId && p.PathId != sourcePathId && p.SubjectId == sourcePath.SubjectId),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        sourcePath.UserId.Should().Be(mentorId);
    }

    [Fact]
    public async Task Handle_SourcePathNotFound_ReturnsNotFound()
    {
        var studentId = NewId.NextGuid();
        var shareId = NewId.NextGuid();

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = NewId.NextGuid(),
            MentorId = NewId.NextGuid(),
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new AcceptLearningPathShareCommand(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
