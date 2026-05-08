using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class AcceptLearningPathShareCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<ILearningPathSharePathSyncService> _mockPathSyncService;
    private readonly AcceptLearningPathShareCommandHandler _handler;

    public AcceptLearningPathShareCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockPathSyncService = new Mock<ILearningPathSharePathSyncService>();
        _handler = new AcceptLearningPathShareCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockDateTimeProvider.Object,
            _mockPathSyncService.Object);
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

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Mentor Path",
            Description = "Desc",
            StartDate = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 01, 31, 0, 0, 0, DateTimeKind.Utc),
            Status = LearningPathStatus.Active.ToString(),
            CreatedByType = true,
            Language = LanguageSelection.VietNamese,
            ComplexityLevel = ComplexityLevel.Beginner
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

        var studentPathId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        var fixedUtcNow = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(fixedUtcNow);
        _mockPathSyncService
            .Setup(x => x.ClonePathForStudentAsync(sourcePath, studentId, fixedUtcNow.AddHours(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(studentPathId);
        _mockContext.Setup(x => x.Users).Returns(usersDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new AcceptLearningPathShareCommand(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        share.Status.Should().Be(LearningPathShareStatus.Accepted);
        share.RespondedAt.Should().NotBeNull();
        share.RespondedAt.Should().Be(fixedUtcNow.AddHours(7));
        share.AcceptedPathId.Should().Be(studentPathId);
        share.SourceVersionAtAccept.Should().Be(sourcePath.VersionNumber);
        share.SourceSnapshotJson.Should().NotBeNullOrWhiteSpace();
        share.IgnoredSourceVersion.Should().BeNull();
        share.LastNotifiedSourceVersion.Should().BeNull();
        share.IsTrackingEnabled.Should().BeTrue();
        result.Value!.AcceptedPathId.Should().Be(studentPathId);
        result.Value.SourceVersionAtAccept.Should().Be(sourcePath.VersionNumber);
        _mockPathSyncService.Verify(
            x => x.ClonePathForStudentAsync(sourcePath, studentId, fixedUtcNow.AddHours(7), It.IsAny<CancellationToken>()),
            Times.Once);

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
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc));
        _mockContext.Setup(x => x.Users).Returns(new List<User> { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new AcceptLearningPathShareCommand(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
        _mockPathSyncService.Verify(
            x => x.ClonePathForStudentAsync(It.IsAny<LearningPath>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
