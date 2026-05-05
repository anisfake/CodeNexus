using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.EnrollInPublishedLearningPath;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class EnrollInPublishedLearningPathCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<ILearningPathSharePathSyncService> _mockPathSyncService;
    private readonly EnrollInPublishedLearningPathCommandHandler _handler;

    public EnrollInPublishedLearningPathCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockPathSyncService = new Mock<ILearningPathSharePathSyncService>();
        _handler = new EnrollInPublishedLearningPathCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockDateTimeProvider.Object,
            _mockPathSyncService.Object);
    }

    [Fact]
    public async Task Handle_PublishedPath_ClonesPathAndCreatesAcceptedShare()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var enrolledPathId = NewId.NextGuid();
        var fixedUtcNow = new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc);

        var student = new User { UserId = studentId, Username = "student", Role = new Role { RoleName = "Student" } };
        var sourcePath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Published Path - ver 1.0",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var usersDb = new List<User> { student }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { sourcePath }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(fixedUtcNow);
        _mockPathSyncService
            .Setup(x => x.ClonePathForStudentAsync(sourcePath, studentId, fixedUtcNow.AddHours(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(enrolledPathId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new EnrollInPublishedLearningPathCommand(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnrolledPathId.Should().Be(enrolledPathId);
        result.Value.VersionNumber.Should().Be(1.0m);
        _mockContext.Verify(x => x.LearningPathShares.AddAsync(
            It.Is<LearningPathShare>(s =>
                s.PathId == pathId &&
                s.StudentId == studentId &&
                s.MentorId == mentorId &&
                s.Status == LearningPathShareStatus.Accepted &&
                s.AcceptedPathId == enrolledPathId &&
                s.SourceVersionAtAccept == 1.0m &&
                s.IsTrackingEnabled),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PathNotPublished_ReturnsFailure()
    {
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var student = new User { UserId = studentId, Username = "student", Role = new Role { RoleName = "Student" } };
        var draftPath = new LearningPath
        {
            PathId = pathId,
            UserId = NewId.NextGuid(),
            Status = LearningPathStatus.Draft.ToString(),
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var usersDb = new List<User> { student }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { draftPath }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);

        var result = await _handler.Handle(new EnrollInPublishedLearningPathCommand(pathId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_PUBLISHED");
    }

    [Fact]
    public async Task Handle_AlreadyEnrolled_ReturnsAlreadyEnrolled()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var student = new User { UserId = studentId, Username = "student", Role = new Role { RoleName = "Student" } };
        var sourcePath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };
        var existingShare = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            StudentId = studentId,
            MentorId = mentorId,
            Status = LearningPathShareStatus.Accepted
        };

        var usersDb = new List<User> { student }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { sourcePath }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare> { existingShare }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);

        var result = await _handler.Handle(new EnrollInPublishedLearningPathCommand(pathId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ALREADY_ENROLLED");
    }

    [Fact]
    public async Task Handle_PathNotFound_ReturnsFailure()
    {
        var studentId = NewId.NextGuid();
        var student = new User { UserId = studentId, Username = "student", Role = new Role { RoleName = "Student" } };

        var usersDb = new List<User> { student }.BuildMockDbSet();
        var pathsDb = new List<LearningPath>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);

        var result = await _handler.Handle(new EnrollInPublishedLearningPathCommand(NewId.NextGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Throws<InvalidOperationException>();

        var result = await _handler.Handle(new EnrollInPublishedLearningPathCommand(NewId.NextGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }
}
