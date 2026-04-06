using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class UpdateMentorLearningPathDraftCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly UpdateMentorLearningPathDraftCommandHandler _handler;

    public UpdateMentorLearningPathDraftCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new UpdateMentorLearningPathDraftCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_NoChanges_DoesNotPersistAndReturnsSuccess()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var startDate = new DateTime(2026, 04, 01, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 04, 30, 0, 0, 0, DateTimeKind.Utc);
        var lessonDay = new DateTime(2026, 04, 03, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "TypeScript",
            CreatedByUserId = mentorId
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            Duration = GoalDuration.OneMonth,
            IsSystemDefined = false,
            IsActive = true
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = pathId,
            Title = "Chapter 1",
            OrderIndex = 0,
            StartDate = startDate,
            EndDate = endDate,
            EstimatedDays = 30,
            IsDeleted = false,
            Lessons = new List<Lesson>
            {
                new()
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = NewId.NextGuid(),
                    Title = "Lesson 1",
                    OrderIndex = 0,
                    LessonDay = lessonDay,
                    IsDeleted = false,
                    Content = "Existing content",
                    Quizzes = new List<Quiz>
                    {
                        new()
                        {
                            QuizId = NewId.NextGuid(),
                            Title = "Quiz 1",
                            Description = "Quiz desc",
                            IsDeleted = false
                        }
                    }
                }
            },
            Tasks = new List<CodeNexus.Domain.Entities.Tasks>
            {
                new()
                {
                    TaskId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Task 1",
                    Description = "Task desc",
                    TaskType = TaskType.Practice,
                    Priority = TaskPriority.Medium,
                    Status = TaskStatus_.Pending,
                    QuizQuestionsJson = "[]"
                }
            }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "Draft TS",
            Description = "desc",
            StartDate = startDate,
            EndDate = endDate,
            Status = LearningPathStatus.Draft.ToString(),
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.English,
            Chapters = new List<Chapter> { chapter },
            LearningPathGoals = new List<LearningPathGoal>
            {
                new()
                {
                    PathId = pathId,
                    GoalId = goalId,
                    Weight = 100m,
                    Goal = goal
                }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject> { subject }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new List<GoalEntity> { goal }.BuildMockDbSet().Object);

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English,
            "  Draft TS  ",
            "desc",
            startDate,
            endDate,
            new List<ManualChapterRequest>
            {
                new("  Chapter 1  ", startDate, endDate, 30, new List<ManualLessonRequest>
                {
                    new("  Lesson 1  ", lessonDay)
                })
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.ChapterDtos.Should().HaveCount(1);
        result.Value.ChapterDtos[0].Lessons.Should().HaveCount(1);
        result.Value.ChapterDtos[0].Lessons[0].Quizzes.Should().HaveCount(1);
        result.Value.ChapterDtos[0].Tasks.Should().HaveCount(1);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
