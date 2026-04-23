using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.RepublishLearningPath;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using MediatR;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class RepublishLearningPathCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly RepublishLearningPathCommandHandler _handler;

    public RepublishLearningPathCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPublisher = new Mock<IPublisher>();
        _handler = new RepublishLearningPathCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockPublisher.Object);
    }

    private void SetupMocks(Guid mentorId, LearningPath learningPath)
    {
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { learningPath }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Handle_DraftPath_SetsStatusToPublishedAndReturnsSuccess()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            Title = "Test Path",
            Status = LearningPathStatus.Draft.ToString()
        };

        SetupMocks(mentorId, learningPath);

        var result = await _handler.Handle(new RepublishLearningPathCommand(pathId, false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.Status.Should().Be(LearningPathStatus.Published.ToString());
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Throws<Exception>();

        var result = await _handler.Handle(new RepublishLearningPathCommand(NewId.NextGuid(), false, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var usersDb = new List<User>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);

        var result = await _handler.Handle(new RepublishLearningPathCommand(NewId.NextGuid(), false, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonMentorRole_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();
        var student = new User { UserId = userId, Username = "student", Role = new Role { RoleName = "Student" } };
        var usersDb = new List<User> { student }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);

        var result = await _handler.Handle(new RepublishLearningPathCommand(NewId.NextGuid(), false, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_LearningPathNotFound_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);

        var result = await _handler.Handle(new RepublishLearningPathCommand(NewId.NextGuid(), false, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_PathOwnedByOtherMentor_ReturnsAccessDenied()
    {
        var mentorId = NewId.NextGuid();
        var otherMentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = otherMentorId,
            Title = "Other Mentor Path",
            Status = LearningPathStatus.Draft.ToString()
        };

        var mentor = new User { UserId = mentorId, Username = "mentor", Role = new Role { RoleName = "Mentor" } };
        var usersDb = new List<User> { mentor }.BuildMockDbSet();
        var pathsDb = new List<LearningPath> { learningPath }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);

        var result = await _handler.Handle(new RepublishLearningPathCommand(pathId, false, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_PublishedPath_SetsStatusToPublishedAndReturnsSuccess()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            Title = "Published Path - ver 1.0",
            VersionNumber = 1.0m,
            Status = LearningPathStatus.Published.ToString()
        };

        SetupMocks(mentorId, learningPath);

        var result = await _handler.Handle(new RepublishLearningPathCommand(pathId, false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.Status.Should().Be(LearningPathStatus.Published.ToString());
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithIncreaseVersionMinor_IncrementsVersionByPoint1()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            Title = "Test Path - ver 1.0",
            VersionNumber = 1.0m,
            Status = LearningPathStatus.Draft.ToString()
        };

        SetupMocks(mentorId, learningPath);

        var result = await _handler.Handle(new RepublishLearningPathCommand(pathId, true, DraftVersionUpdateType.Minor), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(1.1m);
        learningPath.Status.Should().Be(LearningPathStatus.Published.ToString());
    }

    [Fact]
    public async Task Handle_WithIncreaseVersionMajor_IncrementsVersionToNextWhole()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            Title = "Test Path - ver 1.2",
            VersionNumber = 1.2m,
            Status = LearningPathStatus.Draft.ToString()
        };

        SetupMocks(mentorId, learningPath);

        var result = await _handler.Handle(new RepublishLearningPathCommand(pathId, true, DraftVersionUpdateType.Major), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(2.0m);
        learningPath.Status.Should().Be(LearningPathStatus.Published.ToString());
    }

    [Fact]
    public async Task Handle_WithoutIncreaseVersion_KeepsVersionUnchanged()
    {
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            Title = "Test Path - ver 2.0",
            VersionNumber = 2.0m,
            Status = LearningPathStatus.Draft.ToString()
        };

        SetupMocks(mentorId, learningPath);

        var result = await _handler.Handle(new RepublishLearningPathCommand(pathId, false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(2.0m);
    }
}
