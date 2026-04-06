using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathDraftDetail;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetLearningPathDraftDetailQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetLearningPathDraftDetailQueryHandler _handler;

    public GetLearningPathDraftDetailQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetLearningPathDraftDetailQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_OwnDraftPath_ReturnsDetail()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" };
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = role };
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#", CreatedByUserId = mentorId };
        var goal = new GoalEntity { GoalId = NewId.NextGuid(), Title = "Goal", Duration = GoalDuration.OneWeek, IsSystemDefined = false, IsActive = true };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            User = mentor,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Draft path",
            Status = LearningPathStatus.Draft.ToString(),
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goal.GoalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    IsDeleted = false,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = NewId.NextGuid(),
                            Title = "Lesson 1",
                            LessonDay = DateTime.UtcNow,
                            IsDeleted = false,
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
                            Status = TaskStatus_.Pending,
                            QuizQuestionsJson = "[]"
                        }
                    }
                }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathDraftDetailQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.Status.Should().Be(LearningPathStatus.Draft.ToString());
        result.Value.ChapterDtos.Should().HaveCount(1);
        result.Value.Chapters.Should().HaveCount(1);
        result.Value.ChapterDtos[0].Id.Should().Be(result.Value.ChapterDtos[0].ChapterId);
        result.Value.ChapterDtos[0].Lessons.Should().HaveCount(1);
        result.Value.ChapterDtos[0].Lessons[0].Quizzes.Should().HaveCount(1);
        result.Value.ChapterDtos[0].Lessons[0].Quizzes[0].QuizId.Should().Be(result.Value.ChapterDtos[0].Lessons[0].Quizzes[0].QuizzId);
        result.Value.ChapterDtos[0].Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonDraftPath_ReturnsInvalidStatus()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Subject = new Subject { SubjectId = NewId.NextGuid(), Name = "Math", CreatedByUserId = mentorId },
            User = mentor,
            Status = LearningPathStatus.Active.ToString(),
            Chapters = new List<Chapter>(),
            LearningPathGoals = new List<LearningPathGoal>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathDraftDetailQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_STATUS");
    }
}
