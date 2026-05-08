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
            x => x.RebuildCurrentPathFromSourceAsync(
                It.IsAny<LearningPath>(),
                It.IsAny<LearningPath>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlySet<string>>()),
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

    [Fact]
    public async Task Handle_UpdateCurrentToLatest_PassesOnlyContentChangedLessonKeysAndRefreshesSnapshot()
    {
        var shareId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();
        var acceptedPathId = NewId.NextGuid();
        var now = new DateTime(2026, 04, 06, 8, 0, 0, DateTimeKind.Utc);

        var baselinePath = BuildPath(
            sourcePathId,
            mentorId,
            1,
            ("Intro", "same intro"),
            ("Deep Dive", "old deep dive"));

        var sourcePath = BuildPath(
            sourcePathId,
            mentorId,
            2,
            ("Intro", "same intro"),
            ("Deep Dive", "new deep dive"));

        var acceptedPath = BuildPath(
            acceptedPathId,
            studentId,
            1,
            ("Intro", "student local intro"),
            ("Deep Dive", "student local deep dive"));

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = mentorId,
            StudentId = studentId,
            AcceptedPathId = acceptedPathId,
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            SourceSnapshotJson = LearningPathShareSourceSnapshotHelper.CreateSnapshotJson(baselinePath),
            IsTrackingEnabled = true,
            SentAt = now.AddHours(-2)
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(now);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new[] { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath, acceptedPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockPathSyncService
            .Setup(x => x.RebuildCurrentPathFromSourceAsync(
                acceptedPath,
                sourcePath,
                studentId,
                now.AddHours(7),
                It.IsAny<CancellationToken>(),
                It.Is<IReadOnlySet<string>>(keys =>
                    keys.Count == 1 &&
                    keys.Contains(LearningPathShareSourceSnapshotHelper.BuildLessonKey(0, 1)))))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new ApplyLearningPathShareUpdateCommand(shareId, LearningPathShareUpdateAction.UpdateCurrentToLatest),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        share.SourceVersionAtAccept.Should().Be(2);
        share.LastNotifiedSourceVersion.Should().Be(2);
        share.SourceSnapshotJson.Should().NotBeNullOrWhiteSpace();
        LearningPathShareSourceSnapshotHelper
            .TryGetContentChangedLessonKeys(share.SourceSnapshotJson, sourcePath)!
            .Should()
            .BeEmpty();

        _mockPathSyncService.Verify(
            x => x.RebuildCurrentPathFromSourceAsync(
                acceptedPath,
                sourcePath,
                studentId,
                now.AddHours(7),
                It.IsAny<CancellationToken>(),
                It.Is<IReadOnlySet<string>>(keys =>
                    keys.Count == 1 &&
                    keys.Contains(LearningPathShareSourceSnapshotHelper.BuildLessonKey(0, 1)))),
            Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreateNewFromLatest_RefreshesSnapshot()
    {
        var shareId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();
        var newPathId = NewId.NextGuid();
        var now = new DateTime(2026, 04, 06, 8, 0, 0, DateTimeKind.Utc);

        var baselinePath = BuildPath(sourcePathId, mentorId, 1, ("Intro", "old"));
        var sourcePath = BuildPath(sourcePathId, mentorId, 2, ("Intro", "new"));
        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = mentorId,
            StudentId = studentId,
            AcceptedPathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            SourceSnapshotJson = LearningPathShareSourceSnapshotHelper.CreateSnapshotJson(baselinePath),
            IsTrackingEnabled = true,
            SentAt = now.AddHours(-2)
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(now);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new[] { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockPathSyncService
            .Setup(x => x.ClonePathForStudentAsync(sourcePath, studentId, now.AddHours(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newPathId);

        var result = await _handler.Handle(
            new ApplyLearningPathShareUpdateCommand(shareId, LearningPathShareUpdateAction.CreateNewFromLatest),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        share.AcceptedPathId.Should().Be(newPathId);
        share.SourceVersionAtAccept.Should().Be(2);
        share.SourceSnapshotJson.Should().NotBeNullOrWhiteSpace();
        LearningPathShareSourceSnapshotHelper
            .TryGetContentChangedLessonKeys(share.SourceSnapshotJson, sourcePath)!
            .Should()
            .BeEmpty();
    }

    private static LearningPath BuildPath(
        Guid pathId,
        Guid userId,
        decimal version,
        params (string Title, string Content)[] lessons)
    {
        return new LearningPath
        {
            PathId = pathId,
            UserId = userId,
            SubjectId = NewId.NextGuid(),
            Title = $"Backend Path - ver {version:0.0}",
            VersionNumber = version,
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Chapter Basics",
                    Content = "chapter content",
                    OrderIndex = 0,
                    Lessons = lessons
                        .Select((lesson, index) => new Lesson
                        {
                            LessonId = NewId.NextGuid(),
                            Title = lesson.Title,
                            Content = lesson.Content,
                            OrderIndex = index
                        })
                        .ToList(),
                    Tasks = new List<CodeNexus.Domain.Entities.Tasks>()
                }
            }
        };
    }
}
