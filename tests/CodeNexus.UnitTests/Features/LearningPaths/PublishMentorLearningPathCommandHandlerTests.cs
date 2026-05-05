using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.PublishMentorLearningPath;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using MediatR;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class PublishMentorLearningPathCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly PublishMentorLearningPathCommandHandler _handler;

    public PublishMentorLearningPathCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPublisher = new Mock<IPublisher>();
        _handler = new PublishMentorLearningPathCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockPublisher.Object);
    }

    private PublishMentorLearningPathCommand BuildCommand(Guid pathId, bool increaseVersion = false, Guid? subjectId = null, Guid? goalId = null)
    {
        return new PublishMentorLearningPathCommand(
            pathId,
            increaseVersion,
            increaseVersion ? DraftVersionUpdateType.Minor : null,
            subjectId ?? NewId.NextGuid(),
            new List<LearningPathGoalRequest> { new(goalId ?? NewId.NextGuid(), 100) },
            ComplexityLevel.Beginner,
            LanguageSelection.VietNamese,
            "Test Path",
            null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new List<ManualChapterRequest>
            {
                new("Chapter 1", null, null, 7,
                    new List<ManualLessonRequest>
                    {
                        new("Lesson 1", new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc), null, "Lesson content here")
                    })
            });
    }

    [Fact]
    public async Task Handle_DraftPath_SetsStatusToPublishedAndReturnsSuccess()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();

        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var subject = new Subject { SubjectId = subjectId, Name = "C#" };
        var goal = new GoalEntity { GoalId = goalId, Title = "Goal", IsSystemDefined = false };
        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "Test Path - ver 1.0",
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 1.0m,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { learningPath }.BuildMockDbSet();
        var subjectsDb = new List<Subject> { subject }.BuildMockDbSet();
        var goalsDb = new List<GoalEntity> { goal }.BuildMockDbSet();
        var subjectGoalsDb = new List<SubjectGoal>().BuildMockDbSet();
        var learningPathGoalsDb = new List<LearningPathGoal>().BuildMockDbSet();
        var chaptersDb = new List<Chapter>().BuildMockDbSet();
        var lessonsDb = new List<Lesson>().BuildMockDbSet();
        var quizzesDb = new List<Quiz>().BuildMockDbSet();
        var questionsDb = new List<Questions>().BuildMockDbSet();
        var tasksDb = new List<Domain.Entities.Tasks>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjectsDb.Object);
        _mockContext.Setup(x => x.Goals).Returns(goalsDb.Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(subjectGoalsDb.Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(learningPathGoalsDb.Object);
        _mockContext.Setup(x => x.Chapters).Returns(chaptersDb.Object);
        _mockContext.Setup(x => x.Lessons).Returns(lessonsDb.Object);
        _mockContext.Setup(x => x.Quizzes).Returns(quizzesDb.Object);
        _mockContext.Setup(x => x.Questions).Returns(questionsDb.Object);
        _mockContext.Setup(x => x.Tasks).Returns(tasksDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = BuildCommand(pathId, subjectId: subjectId, goalId: goalId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.Status.Should().Be(LearningPathStatus.Published.ToString());
    }

    [Fact]
    public async Task Handle_IncreaseVersionTrue_PublishesEvent()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();

        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var subject = new Subject { SubjectId = subjectId, Name = "C#" };
        var goal = new GoalEntity { GoalId = goalId, Title = "Goal", IsSystemDefined = false };
        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "Test Path - ver 1.0",
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 1.0m,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { learningPath }.BuildMockDbSet();
        var subjectsDb = new List<Subject> { subject }.BuildMockDbSet();
        var goalsDb = new List<GoalEntity> { goal }.BuildMockDbSet();
        var subjectGoalsDb = new List<SubjectGoal>().BuildMockDbSet();
        var learningPathGoalsDb = new List<LearningPathGoal>().BuildMockDbSet();
        var chaptersDb = new List<Chapter>().BuildMockDbSet();
        var lessonsDb = new List<Lesson>().BuildMockDbSet();
        var quizzesDb = new List<Quiz>().BuildMockDbSet();
        var questionsDb = new List<Questions>().BuildMockDbSet();
        var tasksDb = new List<Domain.Entities.Tasks>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjectsDb.Object);
        _mockContext.Setup(x => x.Goals).Returns(goalsDb.Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(subjectGoalsDb.Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(learningPathGoalsDb.Object);
        _mockContext.Setup(x => x.Chapters).Returns(chaptersDb.Object);
        _mockContext.Setup(x => x.Lessons).Returns(lessonsDb.Object);
        _mockContext.Setup(x => x.Quizzes).Returns(quizzesDb.Object);
        _mockContext.Setup(x => x.Questions).Returns(questionsDb.Object);
        _mockContext.Setup(x => x.Tasks).Returns(tasksDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = BuildCommand(pathId, increaseVersion: true, subjectId: subjectId, goalId: goalId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mockPublisher.Verify(x => x.Publish(It.IsAny<LearningPathDraftVersionUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_IncreaseVersionFalse_DoesNotPublishEvent()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();

        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var subject = new Subject { SubjectId = subjectId, Name = "C#" };
        var goal = new GoalEntity { GoalId = goalId, Title = "Goal", IsSystemDefined = false };
        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "Test Path - ver 1.0",
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 1.0m,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { learningPath }.BuildMockDbSet();
        var subjectsDb = new List<Subject> { subject }.BuildMockDbSet();
        var goalsDb = new List<GoalEntity> { goal }.BuildMockDbSet();
        var subjectGoalsDb = new List<SubjectGoal>().BuildMockDbSet();
        var learningPathGoalsDb = new List<LearningPathGoal>().BuildMockDbSet();
        var chaptersDb = new List<Chapter>().BuildMockDbSet();
        var lessonsDb = new List<Lesson>().BuildMockDbSet();
        var quizzesDb = new List<Quiz>().BuildMockDbSet();
        var questionsDb = new List<Questions>().BuildMockDbSet();
        var tasksDb = new List<Domain.Entities.Tasks>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjectsDb.Object);
        _mockContext.Setup(x => x.Goals).Returns(goalsDb.Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(subjectGoalsDb.Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(learningPathGoalsDb.Object);
        _mockContext.Setup(x => x.Chapters).Returns(chaptersDb.Object);
        _mockContext.Setup(x => x.Lessons).Returns(lessonsDb.Object);
        _mockContext.Setup(x => x.Quizzes).Returns(quizzesDb.Object);
        _mockContext.Setup(x => x.Questions).Returns(questionsDb.Object);
        _mockContext.Setup(x => x.Tasks).Returns(tasksDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = BuildCommand(pathId, increaseVersion: false, subjectId: subjectId, goalId: goalId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mockPublisher.Verify(x => x.Publish(It.IsAny<LearningPathDraftVersionUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PathNotInDraftStatus_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();

        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "Published Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { learningPath }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);

        var command = BuildCommand(pathId, subjectId: subjectId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PATH_NOT_IN_DRAFT_STATUS");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Throws<InvalidOperationException>();

        var result = await _handler.Handle(BuildCommand(NewId.NextGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_PathNotFound_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };

        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);

        var result = await _handler.Handle(BuildCommand(NewId.NextGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonMentorUser_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();
        var user = new User { UserId = userId, Username = "student", Role = new Role { RoleName = "Student" } };

        var usersDb = new List<User> { user }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);

        var result = await _handler.Handle(BuildCommand(NewId.NextGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
