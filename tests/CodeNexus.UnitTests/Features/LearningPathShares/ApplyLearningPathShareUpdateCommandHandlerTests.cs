using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class ApplyLearningPathShareUpdateCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<ILearningPathSharePathSyncService> _mockPathSyncService;
    private readonly ApplyLearningPathShareUpdateCommandHandler _handler;

    public ApplyLearningPathShareUpdateCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockPathSyncService = new Mock<ILearningPathSharePathSyncService>();

        _handler = new ApplyLearningPathShareUpdateCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockDateTimeProvider.Object,
            _mockPathSyncService.Object);
    }

    [Fact]
    public async Task Handle_DisableUpdateNotifications_DisablesTrackingForShare()
    {
        var shareId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = NewId.NextGuid(),
            StudentId = studentId,
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            IsTrackingEnabled = true,
            SentAt = new DateTime(2026, 04, 06, 8, 0, 0, DateTimeKind.Utc)
        };

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = NewId.NextGuid(),
            SubjectId = NewId.NextGuid(),
            Title = "Backend Path - ver 3",
            VersionNumber = 3,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new[] { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new ApplyLearningPathShareUpdateCommand(shareId, LearningPathShareUpdateAction.DisableUpdateNotifications),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        share.IsTrackingEnabled.Should().BeFalse();
        share.IgnoredSourceVersion.Should().Be(3);
        share.LastNotifiedSourceVersion.Should().Be(3);
        share.SourceVersionAtAccept.Should().Be(1);

        _mockPathSyncService.Verify(
            x => x.ClonePathForStudentAsync(It.IsAny<LearningPath>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockPathSyncService.Verify(
            x => x.RebuildCurrentPathFromSourceAsync(It.IsAny<LearningPath>(), It.IsAny<LearningPath>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoNewVersion_ReturnsFailure()
    {
        var shareId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = NewId.NextGuid(),
            StudentId = studentId,
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 3,
            IsTrackingEnabled = true,
            SentAt = new DateTime(2026, 04, 06, 8, 0, 0, DateTimeKind.Utc)
        };

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = NewId.NextGuid(),
            SubjectId = NewId.NextGuid(),
            Title = "Backend Path - ver 3",
            VersionNumber = 3,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new[] { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new ApplyLearningPathShareUpdateCommand(shareId, LearningPathShareUpdateAction.DisableUpdateNotifications),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_NEW_VERSION_AVAILABLE");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
