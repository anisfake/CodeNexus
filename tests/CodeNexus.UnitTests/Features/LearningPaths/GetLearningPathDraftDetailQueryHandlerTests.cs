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
        var goal = new GoalEntity { GoalId = NewId.NextGuid(), Title = "Goal", Duration = GoalDuration.OneWeek, IsSystemDefined = false};

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
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathDraftDetailQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.Status.Should().Be(LearningPathStatus.Draft.ToString());
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

    [Fact]
    public async Task Handle_DeletedQuestion_ExcludedFromResponse()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();
        var quizId = NewId.NextGuid();
        var activeQuestionId = NewId.NextGuid();
        var deletedQuestionId = NewId.NextGuid();

        var role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" };
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = role };
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#", CreatedByUserId = mentorId };
        var goal = new GoalEntity { GoalId = NewId.NextGuid(), Title = "Goal", Duration = GoalDuration.OneWeek, IsSystemDefined = false};

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
                    ChapterId = chapterId,
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = lessonId,
                            ChapterId = chapterId,
                            Title = "Lesson 1",
                            LessonDay = DateTime.UtcNow.Date,
                            Quizzes = new List<Quiz>
                            {
                                new()
                                {
                                    QuizId = quizId,
                                    LessonId = lessonId,
                                    Title = "Quiz 1",
                                    Questions = new List<Questions>
                                    {
                                        new()
                                        {
                                            QuestionId = activeQuestionId,
                                            QuizId = quizId,
                                            QuestionText = "Active question",
                                            Type = QuestionType.SingleChoice,
                                            Points = 1,
                                            OrderIndex = 0,
                                            IsDeleted = false
                                        },
                                        new()
                                        {
                                            QuestionId = deletedQuestionId,
                                            QuizId = quizId,
                                            QuestionText = "Deleted question",
                                            Type = QuestionType.SingleChoice,
                                            Points = 1,
                                            OrderIndex = 1,
                                            IsDeleted = true
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Tasks = new List<CodeNexus.Domain.Entities.Tasks>()
                }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathDraftDetailQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var questions = result.Value!.ChapterDtos[0].Lessons[0].Quizzes[0].Questions!;
        questions.Should().HaveCount(1);
        questions[0].QuestionId.Should().Be(activeQuestionId);
    }
}
